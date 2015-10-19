﻿#define DEBUG_CC2D_RAYS
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// The contoller for characters.
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class CharacterController2D : MonoBehaviour
{
	#region internal types
	/// <summary>
	/// The origins of the characters raycast points.
	/// </summary>
	private struct CharacterRaycastOrigins
	{
		public Vector3 topRight;
		public Vector3 topLeft;
		public Vector3 bottomRight;
		public Vector3 bottomLeft;
	}

	/// <summary>
	/// The states of each collision in the character.
	/// </summary>
	public class CharacterCollisionState2D
	{
		public bool right;
		public bool left;
		public bool above;
		public bool below;
		public bool becameGroundedThisFrame;
		public bool movingDownSlope;
		public float slopeAngle;

		/// <summary>
		/// Returns if the collision is active.
		/// </summary>
		/// <returns>Boolean</returns>
		public bool hasCollision()
		{
			return below || right || left || above;
		}

		/// <summary>
		/// Resets the collision.
		/// </summary>
		public void reset()
		{
			right = left = above = below = becameGroundedThisFrame = movingDownSlope = false;
			slopeAngle = 0f;
		}

		/// <summary>
		/// Returns the character collision states as a formatted string.
		/// </summary>
		/// <returns>String</returns>
		public override string ToString()
		{
			return string.Format("[CharacterCollisionState2D] r: {0}, l: {1}, a: {2}, b: {3}, movingDownSlope: {4}, angle: {5}",
								 right, left, above, below, movingDownSlope, slopeAngle);
		}
	}
	#endregion
	
	#region events, properties and fields
	/// <summary>
	/// Gets the event if the controller collided.
	/// </summary>
	public event Action<RaycastHit2D> onControllerCollidedEvent;
	/// <summary>
	/// Gets the event if the trigger entered a collision.
	/// </summary>
	public event Action<Collider2D> onTriggerEnterEvent;
	/// <summary>
	/// Gets the event if the trigger stayed on a collision.
	/// </summary>
	public event Action<Collider2D> onTriggerStayEvent;
	/// <summary>
	/// Gets the event if the trigger exited a collision.
	/// </summary>
	public event Action<Collider2D> onTriggerExitEvent;
	
	/// <summary>
	/// Toggles if the RigidBody2D methods should be used for movement or if Transform.Translate will be used. All the usual Unity rules for physics based movement apply when true
	/// such as getting your input in Update and only calling move in FixedUpdate amonst others.
	/// </summary>
	public bool usePhysicsForMovement = false;

	/// <summary>
	/// Defines how far in from the edges of the collider rays are cast from. If cast with a 0 extent it will often result in ray hits that are
	/// not desired (for example a foot collider casting horizontally from directly on the surface can result in a hit).
	/// </summary>
	[SerializeField]
	[Range(0.001f, 0.3f)]
	private float _skinWidth = 0.02f;

	/// <summary>
	/// Defines how far in from the edges of the collider rays are cast from. If cast with a 0 extent it will often result in ray hits that are
	/// not desired (for example a foot collider casting horizontally from directly on the surface can result in a hit).
	/// </summary>
	public float skinWidth
	{
		get
		{
			return _skinWidth;
		}
		set
		{
			_skinWidth = value;
			recalculateDistanceBetweenRays();
		}
	}
	
	/// <summary>
	/// Mask with all layers that the player should interact with.
	/// </summary>
	public LayerMask platformMask = 0;

	/// <summary>
	/// Mask with all layers that should act as one-way platforms. Note that one-way platforms should always be EdgeCollider2Ds. This is private because it does not support being
	/// updated anytime outside of the inspector for now.
	/// </summary>
	[SerializeField]
	private LayerMask oneWayPlatformMask = 0;
	
	/// <summary>
	/// The max slope angle that the CC2D can climb.
	/// </summary>
	/// <value>The slope limit.</value>
	[Range(0, 90f)]
	public float slopeLimit = 30f;
	
	/// <summary>
	/// Curve for multiplying speed based on slope (negative = down slope and positive = up slope).
	/// </summary>
	public AnimationCurve slopeSpeedMultiplier = new AnimationCurve(new Keyframe(-90, 1.5f), new Keyframe(0, 1), new Keyframe(90, 0));

	/// <summary>
	/// The total number of horizontal rays.
	/// </summary>
	[Range(2, 20)]
	public int totalHorizontalRays = 8;
	[Range(2, 20)]
	public int totalVerticalRays = 4;


	/// <summary>
	/// This is used to calculate the downward ray that is cast to check for slopes. We use the somewhat arbitray value 75 degrees
	/// to calcuate the lenght of the ray that checks for slopes.
	/// </summary>
	// Seanba: Disable this an introduce a new variable to manipulate in the editor
	//private float _slopeLimitTangent = Mathf.Tan( 75f * Mathf.Deg2Rad );
	public float SlopeRayLength = 2.0f;


	/// <summary>
	/// If true, a new GameObject named CC2DTriggerHelper will be created in Awake and latched on via a DistanceJoint2D
	/// to the player so that trigger messages can be received.
	/// </summary>
	public bool createTriggerHelperGameObject = false;

	/// <summary>
	/// The scale of the trigger helper box collider.
	/// </summary>
	[Range(0.8f, 0.999f)]
	public float triggerHelperBoxColliderScale = 0.95f;

	/// <summary>
	/// The transform of the character controller 2D.
	/// </summary>
	[HideInInspector]
	public new Transform transform;
	/// <summary>
	/// The box collider of the character controller 2D.
	/// </summary>
	[HideInInspector]
	public BoxCollider2D boxCollider;
	/// <summary>
	/// The rigid body 2D of the character controller 2D
	/// </summary>
	[HideInInspector]
	public Rigidbody2D rigidBody2D;

	/// <summary>
	/// The collision state of the character controller 2D.
	/// </summary>
	[HideInInspector]
	[NonSerialized]
	public CharacterCollisionState2D collisionState = new CharacterCollisionState2D();
	/// <summary>
	/// The velocity of the character controller 2D
	/// </summary>
	[HideInInspector]
	[NonSerialized]
	public Vector3 velocity;
	/// <summary>
	/// Gets or sets if the character controller is grounded.
	/// </summary>
	public bool isGrounded
	{
		get
		{
			return collisionState.below;
		}
	}

	/// <summary>
	/// Unknown.
	/// </summary>
	private const float kSkinWidthFloatFudgeFactor = 0.001f;
	#endregion


	/// <summary>
	/// holder for our raycast origin corners (TR, TL, BR, BL)
	/// </summary>
	private CharacterRaycastOrigins _raycastOrigins;

	/// <summary>
	/// stores our raycast hit during movement
	/// </summary>
	//private RaycastHit2D _raycastHit;

	/// <summary>
	/// stores any raycast hits that occur this frame. we have to store them in case we get a hit moving
	/// horizontally and vertically so that we can send the events after all collision state is set
	/// </summary>
	private List<RaycastHit2D> _raycastHitsThisFrame = new List<RaycastHit2D>(2);

	// horizontal/vertical movement data
	/// <summary>
	/// The vertical distance between rays.
	/// </summary>
	private float _verticalDistanceBetweenRays;
	/// <summary>
	/// The horizontal distance between rays.
	/// </summary>
	private float _horizontalDistanceBetweenRays;
	#region Monobehaviour

	/// <summary>
	/// Runs when the scene awakes.
	/// </summary>
	void Awake()
	{
		// add our one-way platforms to our normal platform mask so that we can land on them from above
		platformMask |= oneWayPlatformMask;

		// cache some components
		transform = GetComponent<Transform>();
		boxCollider = GetComponent<BoxCollider2D>();
		rigidBody2D = GetComponent<Rigidbody2D>();

		// we dont have a bounds property until Unity 4.5+ so we really don't need the BoxCollider2D to be active since we just use
		// it for it's size and center properties so might as well remove it from play
		boxCollider.enabled = false;

		if (createTriggerHelperGameObject)
		{
			createTriggerHelper();
		}

		// here, we trigger our properties that have setters with bodies
		skinWidth = _skinWidth;
	}

	/// <summary>
	/// Checks if the trigger entered a collion.
	/// </summary>
	/// <param name="col">The collider to check.</param>
	public void OnTriggerEnter2D(Collider2D col)
	{
		if (onTriggerEnterEvent != null)
		{
			onTriggerEnterEvent(col);
		}
	}

	/// <summary>
	/// Checks if the trigger stayed on a collion.
	/// </summary>
	/// <param name="col">The collider to check.</param>
	public void OnTriggerStay2D(Collider2D col)
	{
		if (onTriggerStayEvent != null)
			onTriggerStayEvent(col);
	}

	/// <summary>
	/// Checks if the trigger exited a collion.
	/// </summary>
	/// <param name="col">The collider to check.</param>
	public void OnTriggerExit2D(Collider2D col)
	{
		if (onTriggerExitEvent != null)
			onTriggerExitEvent(col);
	}
	#endregion
	
	/// <summary>
	/// Draws a ray for debuging.
	/// </summary>
	/// <param name="start">The starting cordinates.</param>
	/// <param name="dir">The direction.</param>
	/// <param name="color">The color to color the ray.</param>
	[System.Diagnostics.Conditional("DEBUG_CC2D_RAYS")]
	private void DrawRay(Vector3 start, Vector3 dir, Color color)
	{
		Debug.DrawRay(start, dir, color);
	}

	#region Public
	/// <summary>
	/// Moves the character controller 2D.
	/// </summary>
	/// <param name="deltaMovement">The delta movement to move the character.</param>
	public void move(Vector3 deltaMovement)
	{
		// save off our current grounded state
		var wasGroundedBeforeMoving = collisionState.below;

		// clear our state
		collisionState.reset();
		_raycastHitsThisFrame.Clear();

		primeRaycastOrigins();

		// Seanba: Do an initial correction on the deltaMovement passed in.
		// On some edge conditions, the deltaMovement passed in is causing us to penetrate geometry
		correctDeltaMovement(ref deltaMovement);

		// first, we check for a slope below us before moving
		// only check slopes if we are going down and grounded
		if (deltaMovement.y < 0 && wasGroundedBeforeMoving)
			handleVerticalSlope(ref deltaMovement);

		// now we check movement in the horizontal dir
		if (deltaMovement.x != 0)
			moveHorizontally(ref deltaMovement);

		// next, check movement in the vertical dir
		if (deltaMovement.y != 0)
			moveVertically(ref deltaMovement);


		// move then update our state
		if (usePhysicsForMovement)
		{
#if UNITY_4_5 || UNITY_4_6
			rigidbody2D.MovePosition( transform.position + deltaMovement );
#else
			GetComponent<Rigidbody2D>().velocity = deltaMovement / Time.fixedDeltaTime;
#endif
			velocity = GetComponent<Rigidbody2D>().velocity;
		}
		else
		{
			transform.Translate(deltaMovement, Space.World);

			// only calculate velocity if we have a non-zero deltaTime
			if (Time.deltaTime > 0)
				velocity = deltaMovement / Time.deltaTime;
		}

		// set our becameGrounded state based on the previous and current collision state
		if (!wasGroundedBeforeMoving && collisionState.below)
			collisionState.becameGroundedThisFrame = true;

		// send off the collision events if we have a listener
		if (onControllerCollidedEvent != null)
		{
			for (var i = 0; i < _raycastHitsThisFrame.Count; i++)
				onControllerCollidedEvent(_raycastHitsThisFrame[i]);
		}
	}

	/// <summary>
	/// Detects if the delta movement is correct and then fixes it.
	/// </summary>
	/// <param name="deltaMovement">The delta moevment to correct.</param>
	// Seanba: Added this function to handle discovered edge cases with collisions (like penetrations)
	private void correctDeltaMovement(ref Vector3 deltaMovement)
	{
		if (deltaMovement.x != 0)
		{
			var isGoingRight = deltaMovement.x > 0;
			var rayDirection = isGoingRight ? Vector2.right : -Vector2.right;
			float rayMagnitude = Mathf.Abs(deltaMovement.x);
			var initialRayOrigin = isGoingRight ? _raycastOrigins.topRight : _raycastOrigins.topLeft;

			RaycastHit2D hit = Physics2D.Raycast(initialRayOrigin, rayDirection, rayMagnitude, this.platformMask);
			if (hit)
			{
				// Minimize our possible deltaMovement along the x-axis
				// This was needed for when we ran up a slope that met a wall. (We'd often penetrate the wall)
				float dx = hit.fraction * rayMagnitude * rayDirection.x;
				deltaMovement.x = dx;
				DrawRay(initialRayOrigin, rayDirection, Color.yellow);
			}
		}
	}

	/// <summary>
	/// This should be called anytime you have to modify the BoxCollider2D at runtime. It will recalculate the distance between the rays used for collision detection.
	/// It is also used in the skinWidth setter in case it is changed at runtime.
	/// </summary>
	public void recalculateDistanceBetweenRays()
	{
		// figure out the distance between our rays in both directions
		// horizontal
		var colliderUseableHeight = boxCollider.size.y * Mathf.Abs(transform.localScale.y) - (2f * _skinWidth);
		_verticalDistanceBetweenRays = colliderUseableHeight / (totalHorizontalRays - 1);

		// vertical
		var colliderUseableWidth = boxCollider.size.x * Mathf.Abs(transform.localScale.x) - (2f * _skinWidth);
		_horizontalDistanceBetweenRays = colliderUseableWidth / (totalVerticalRays - 1);
	}

	/// <summary>
	/// This is called internally if createTriggerHelperGameObject is true. It is provided as a public method
	/// in case you want to grab a handle on the GO created to modify it in some way. Note that by default only
	/// collisions with triggers will be allowed to pass through and fire the events.
	/// </summary>
	public GameObject createTriggerHelper()
	{
		var go = new GameObject("PlayerTriggerHelper");
		go.hideFlags = HideFlags.HideInHierarchy;
		go.layer = gameObject.layer;
		// scale is slightly less so that we don't get trigger messages when colliding with non-triggers
		go.transform.localScale = transform.localScale * triggerHelperBoxColliderScale;

		go.AddComponent<CC2DTriggerHelper>().setParentCharacterController(this);

		var rb = go.AddComponent<Rigidbody2D>();
		rb.mass = 0f;
		rb.gravityScale = 0f;

		var bc = go.AddComponent<BoxCollider2D>();
		bc.size = boxCollider.size;
		bc.isTrigger = true;

		var joint = go.AddComponent<DistanceJoint2D>();
		joint.connectedBody = GetComponent<Rigidbody2D>();
		joint.distance = 0f;

		return go;
	}
	#endregion
	
	#region Private Movement Methods
	/// <summary>
	/// Resets the raycastOrigins to the current extents of the box collider inset by the skinWidth. It is inset
	/// to avoid casting a ray from a position directly touching another collider which results in wonky normal data.
	/// </summary>
	private void primeRaycastOrigins()
	{
		var scaledColliderSize = new Vector2(boxCollider.size.x * Mathf.Abs(transform.localScale.x), boxCollider.size.y * Mathf.Abs(transform.localScale.y)) / 2;
#if UNITY_5_0 || UNITY_5_1
		var scaledCenter = new Vector2( boxCollider.offset.x * transform.localScale.x, boxCollider.offset.y * transform.localScale.y );
#else
		var scaledCenter = new Vector2(boxCollider.offset.x * transform.localScale.x, boxCollider.offset.y * transform.localScale.y);
#endif

		_raycastOrigins.topRight = transform.position + new Vector3(scaledCenter.x + scaledColliderSize.x, scaledCenter.y + scaledColliderSize.y);
		_raycastOrigins.topRight.x -= _skinWidth;
		_raycastOrigins.topRight.y -= _skinWidth;

		_raycastOrigins.topLeft = transform.position + new Vector3(scaledCenter.x - scaledColliderSize.x, scaledCenter.y + scaledColliderSize.y);
		_raycastOrigins.topLeft.x += _skinWidth;
		_raycastOrigins.topLeft.y -= _skinWidth;

		_raycastOrigins.bottomRight = transform.position + new Vector3(scaledCenter.x + scaledColliderSize.x, scaledCenter.y - scaledColliderSize.y);
		_raycastOrigins.bottomRight.x -= _skinWidth;
		_raycastOrigins.bottomRight.y += _skinWidth;

		_raycastOrigins.bottomLeft = transform.position + new Vector3(scaledCenter.x - scaledColliderSize.x, scaledCenter.y - scaledColliderSize.y);
		_raycastOrigins.bottomLeft.x += _skinWidth;
		_raycastOrigins.bottomLeft.y += _skinWidth;
	}

	/// <summary>
	/// We have to use a bit of trickery in this one. The rays must be cast from a small distance inside of our
	/// collider (skinWidth) to avoid zero distance rays which will get the wrong normal. Because of this small offset
	/// we have to increase the ray distance skinWidth then remember to remove skinWidth from deltaMovement before
	/// actually moving the player
	/// </summary>
	private void moveHorizontally(ref Vector3 deltaMovement)
	{
		var isGoingRight = deltaMovement.x > 0;
		var rayDistance = Mathf.Abs(deltaMovement.x) + _skinWidth;
		var rayDirection = isGoingRight ? Vector2.right : -Vector2.right;
		var initialRayOrigin = isGoingRight ? _raycastOrigins.bottomRight : _raycastOrigins.bottomLeft;

		for (var i = 0; i < totalHorizontalRays; i++)
		{
			var ray = new Vector2(initialRayOrigin.x, initialRayOrigin.y + i * _verticalDistanceBetweenRays);

			DrawRay(ray, rayDirection * rayDistance, Color.red);
			RaycastHit2D _raycastHit = Physics2D.Raycast(ray, rayDirection, rayDistance, platformMask & ~oneWayPlatformMask);
			if (_raycastHit)
			{
				// the bottom ray can hit slopes but no other ray can so we have special handling for those cases
				if (i == 0 && handleHorizontalSlope(ref deltaMovement, Vector2.Angle(_raycastHit.normal, Vector2.up), isGoingRight))
				{
					_raycastHitsThisFrame.Add(_raycastHit);
					break;
				}

				// set our new deltaMovement and recalculate the rayDistance taking it into account
				deltaMovement.x = _raycastHit.point.x - ray.x;
				rayDistance = Mathf.Abs(deltaMovement.x);

				// remember to remove the skinWidth from our deltaMovement
				if (isGoingRight)
				{
					deltaMovement.x -= _skinWidth;
					collisionState.right = true;
				}
				else
				{
					deltaMovement.x += _skinWidth;
					collisionState.left = true;
				}

				//ms_MoveDebugger.Move_x = deltaMovement.x;
				//ms_MoveDebugger.Text.AppendFormat("moveHorizontally : delta.x = {0}\n", deltaMovement.x);
				_raycastHitsThisFrame.Add(_raycastHit);

				// we add a small fudge factor for the float operations here. if our rayDistance is smaller
				// than the width + fudge bail out because we have a direct impact
				if (rayDistance < _skinWidth + kSkinWidthFloatFudgeFactor)
					break;
			}
		}
	}
	
	/// <summary>
	/// Handler for horizontal slopes.
	/// </summary>
	/// <param name="deltaMovement">The delta movement to move the player.</param>
	/// <param name="angle">The angle to move at.</param>
	/// <param name="isGoingRight">Checks if the player is moving to the right.</param>
	/// <returns>Boolean</returns>
	private bool handleHorizontalSlope(ref Vector3 deltaMovement, float angle, bool isGoingRight)
	{
		//Debug.Log( "angle: " + angle );
		// disregard 90 degree angles (walls)
		if (Mathf.RoundToInt(angle) == 90)
			return false;

		// if we can walk on slopes and our angle is small enough we need to move up
		if (angle < slopeLimit)
		{
			// we only need to adjust the y movement if we are not jumping
			// TODO: this uses a magic number which isn't ideal!
			if (deltaMovement.y < 0.07f)
			{
				// apply the slopeModifier to slow our movement up the slope
				var slopeModifier = slopeSpeedMultiplier.Evaluate(angle);
				deltaMovement.x *= slopeModifier;

				// we dont set collisions on the sides for this since a slope is not technically a side collision
				if (isGoingRight)
				{
					deltaMovement.x -= _skinWidth;
				}
				else
				{
					deltaMovement.x += _skinWidth;
				}

				// smooth y movement when we climb. we make the y movement equivalent to the actual y location that corresponds
				// to our new x location using our good friend Pythagoras
				deltaMovement.y = Mathf.Abs(Mathf.Tan(angle * Mathf.Deg2Rad) * deltaMovement.x);

				collisionState.below = true;
			}
		}
		else // too steep. get out of here
		{
			Debug.Log(string.Format("TOO STEEP angle = {0}", angle));
			deltaMovement.x = 0;
		}

		return true;
	}

	/// <summary>
	/// Handles the character moving vertically.
	/// </summary>
	/// <param name="deltaMovement">The delta movement to move the character.</param>
	private void moveVertically(ref Vector3 deltaMovement)
	{
		var isGoingUp = deltaMovement.y > 0;
		var rayDistance = Mathf.Abs(deltaMovement.y) + _skinWidth;
		var rayDirection = isGoingUp ? Vector2.up : -Vector2.up;
		var initialRayOrigin = isGoingUp ? _raycastOrigins.topLeft : _raycastOrigins.bottomLeft;

		// apply our horizontal deltaMovement here so that we do our raycast from the actual position we would be in if we had moved
		initialRayOrigin.x += deltaMovement.x;

		// if we are moving up, we should ignore the layers in oneWayPlatformMask
		var mask = platformMask;
		if (isGoingUp)
			mask &= ~oneWayPlatformMask;

		for (var i = 0; i < totalVerticalRays; i++)
		{
			var ray = new Vector2(initialRayOrigin.x + i * _horizontalDistanceBetweenRays, initialRayOrigin.y);

			DrawRay(ray, rayDirection * rayDistance, Color.red);
			RaycastHit2D _raycastHit = Physics2D.Raycast(ray, rayDirection, rayDistance, mask);
			if (_raycastHit)
			{
				// set our new deltaMovement and recalculate the rayDistance taking it into account
				deltaMovement.y = _raycastHit.point.y - ray.y;
				rayDistance = Mathf.Abs(deltaMovement.y);

				// remember to remove the skinWidth from our deltaMovement
				if (isGoingUp)
				{
					deltaMovement.y -= _skinWidth;
					collisionState.above = true;
				}
				else
				{
					deltaMovement.y += _skinWidth;
					collisionState.below = true;
				}

				_raycastHitsThisFrame.Add(_raycastHit);

				// we add a small fudge factor for the float operations here. if our rayDistance is smaller
				// than the width + fudge bail out because we have a direct impact
				if (rayDistance < _skinWidth + kSkinWidthFloatFudgeFactor)
					return;
			}
		}
	}

	/// <summary>
	/// checks the center point under the BoxCollider2D for a slope. If it finds one then the deltaMovement is adjusted so that
	/// the player stays grounded and the slopeSpeedModifier is taken into account to speed up movement.
	/// </summary>
	/// <param name="deltaMovement">Delta movement.</param>
	private void handleVerticalSlope(ref Vector3 deltaMovement)
	{
		// *** Seanba: Note only one check for slopes means part of the sprite can be on the slope but not detected
		// slope check from the center of our collider
		var centerOfCollider = (_raycastOrigins.bottomLeft.x + _raycastOrigins.bottomRight.x) * 0.5f;
		var rayDirection = -Vector2.up;

		// the ray distance is based on our slopeLimit
		// Seanba: This check ray distance ends up being far too large. Try something else.
		//var slopeCheckRayDistance = _slopeLimitTangent * ( _raycastOrigins.bottomRight.x - centerOfCollider );
		var slopeCheckRayDistance = this.SlopeRayLength;

		var slopeRay = new Vector2(centerOfCollider, _raycastOrigins.bottomLeft.y);

		DrawRay(slopeRay, rayDirection * slopeCheckRayDistance, Color.magenta);
		RaycastHit2D _raycastHit = Physics2D.Raycast(slopeRay, rayDirection, slopeCheckRayDistance, platformMask);
		if (_raycastHit)
		{
			// bail out if we have no slope
			var angle = Vector2.Angle(_raycastHit.normal, Vector2.up);
			// Seanba: commenting this out makes our transition from a downslop to flat surface better
			//if( angle == 0 )
			//	return;

			// we are moving down the slope if our normal and movement direction are in the same x direction
			// Seanba: This seems to be a better test for moving down the slope
			//var isMovingDownSlope = Mathf.Sign( _raycastHit.normal.x ) == Mathf.Sign( deltaMovement.x );
			var isMovingDownSlope = angle == 0 || Mathf.Sign(_raycastHit.normal.x) == Mathf.Sign(deltaMovement.x);
			if (isMovingDownSlope)
			{
				// going down we want to speed up in most cases so the slopeSpeedMultiplier curve should be > 1 for negative angles
				var slopeModifier = slopeSpeedMultiplier.Evaluate(-angle);
				deltaMovement.y = _raycastHit.point.y - slopeRay.y;
				deltaMovement.x *= slopeModifier;
				collisionState.movingDownSlope = true;
				collisionState.slopeAngle = angle;
			}
		}
	}
	#endregion
}