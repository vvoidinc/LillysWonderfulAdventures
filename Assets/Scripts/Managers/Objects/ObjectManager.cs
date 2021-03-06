﻿using UnityEngine;

namespace VoidInc.LWA
{
	public class ObjectManager : MonoBehaviour
	{
		/// <summary>
		/// The object type enum to determine the object function.
		/// </summary>
		public enum ObjectType
		{
			Lock,
			ItemBlock,
			ObjectBlock,
			TrapBlock,
			Sign
		}
		#region Object Structures
		/// <summary>
		/// The parameters for the sign.
		/// </summary>
		public struct Sign
		{
			/// <summary>
			/// The type of sign.
			/// </summary>
			public enum SignType
			{
				dialog,
				heal,
				save
			}
			/// <summary>
			/// The variable for the type of sign.
			/// </summary>
			public SignType Type;
			/// <summary>
			/// The dialog for the sign.
			/// </summary>
			public string Dialog;
		}
		/// <summary>
		/// The parameters for the trap block.
		/// </summary>
		public struct TrapBlock
		{
			/// <summary>
			/// The different direction to deploy the trap block.
			/// </summary>
			public enum TrapDeployDir
			{
				above,
				below,
				left,
				right
			}
			/// <summary>
			/// The direction to deploy the trap block.
			/// </summary>
			public TrapDeployDir Direction;
			/// <summary>
			/// The type of trap.
			/// </summary>
			public string Type;
        }
		#endregion
		/// <summary>
		/// If the object has been activated.
		/// </summary>
		public bool Activated = false;
		/// <summary>
		/// If the object has been activated because the player has transitioned.
		/// </summary>
		public bool TransActivated = false;
		/// <summary>
		/// The type of sign that the sign is.
		/// </summary>
		[HideInInspector]
		public Sign SignType;
		/// <summary>
		/// The type of the trap block.
		/// </summary>
		[HideInInspector]
		public TrapBlock TrapBlockType;
		/// <summary>
		/// What type of item is the item.
		/// </summary>
		public ObjectType Type;
		/// <summary>
		/// The identifier for keys to open locks and for locks to be opened by keys.
		/// </summary>
		public string Identifier;
		/// <summary>
		/// The key id to check the identifier with.
		/// </summary>
		public int KeyID;
		/// <summary>
		/// The GameObject to spawn when an item or object block is triggered.
		/// </summary>
		public GameObject GameObjectSpawn;
		/// <summary>
		/// The UUID of the object.
		/// </summary>
		public string UUID;
		/// <summary>
		/// The GameManager class for the items.
		/// </summary>
		private GameManager _GameManager;

		void Awake()
		{
			_GameManager = FindObjectOfType<GameManager>();

			if (GameObjectSpawn != null)
			{
				GameObjectSpawn.transform.parent = gameObject.transform;
				GameObjectSpawn.transform.localPosition = Vector3.zero;
				GameObjectSpawn.SetActive(false);
			}
		}

		/// <summary>
		/// Activates the objects when the player is activating them.
		/// </summary>
		void ActivateObject(PlayerController2D player)
		{
			switch (Type)
			{
				case ObjectType.ItemBlock:
					TriggerItemBlock(player);
					break;
				case ObjectType.Lock:
					CheckLock(player);
					break;
				case ObjectType.ObjectBlock:
					TriggerObjectBlock(player);
					break;
				case ObjectType.Sign:
					TriggerSign(player);
					break;
				case ObjectType.TrapBlock:
					TriggerTrapBlock(player);
					break;
				default:
					break;
			}
		}

		private void CheckLock()
		{
			if (TransActivated && Activated)
			{
				Destroy(gameObject);
			}
		}

		private void CheckLock(PlayerController2D player)
		{
			if (!Activated && !TransActivated)
			{
				if (_GameManager.GameDataManager.Keys >= 1 && _GameManager.GameDataManager.KeyIdentifiers.ContainsKey(KeyID) && Identifier == _GameManager.GameDataManager.KeyIdentifiers[KeyID])
				{
					Destroy(gameObject);
					_GameManager.GameDataManager.DestroyedGameObjects.Add(UUID);
					_GameManager.GameDataManager.Keys -= 1;
					_GameManager.GameDataManager.KeyIdentifiers.Remove(KeyID);
				}
			}
		}

		private void TriggerItemBlock()
		{
			if (TransActivated && Activated)
			{
				Destroy(GameObjectSpawn);
			}
		}

		private void TriggerItemBlock(PlayerController2D player)
		{
			if (!Activated && !TransActivated)
			{
				if (player.Controller.collisionState.above)
				{
					GameObjectSpawn.SetActive(true);
					GameObjectSpawn.transform.localPosition = new Vector3(0, -32, 0);
				}

				Activated = true;
			}
		}

		private void TriggerSign(PlayerController2D player)
		{
			if (SignType.Type == Sign.SignType.dialog)
			{

			}
		}

		private void TriggerTrapBlock(PlayerController2D player)
		{
			if (!Activated && !TransActivated)
			{
				switch (TrapBlockType.Direction)
				{
					case TrapBlock.TrapDeployDir.above:
						if (player.Controller.collisionState.below)
						{
							Debug.Log("TrapBlock: Activated()");
						}
						break;
					case TrapBlock.TrapDeployDir.below:
						if (player.Controller.collisionState.above)
						{
							Debug.Log("TrapBlock: Activated()");
						}
						break;
					case TrapBlock.TrapDeployDir.left:
						if (player.Controller.collisionState.right)
						{
							Debug.Log("TrapBlock: Activated()");
						}
						break;
					case TrapBlock.TrapDeployDir.right:
						if (player.Controller.collisionState.left)
						{
							Debug.Log("TrapBlock: Activated()");
						}
						break;
				}

				Activated = true;
			}
		}

		private void TriggerObjectBlock()
		{
			if (Activated && TransActivated)
			{
				Destroy(GameObjectSpawn);
			}
		}

		private void TriggerObjectBlock(PlayerController2D player)
		{
			if (!Activated && !TransActivated)
			{
				Activated = true;
			}
		}

		public void DoUpdate()
		{
			if (Type != ObjectType.Sign)
			{
				Activated = true;
				TransActivated = true;

				switch (Type)
				{
					case ObjectType.Lock:
						CheckLock();
						break;
					case ObjectType.ItemBlock:
						TriggerItemBlock();
						break;
					case ObjectType.ObjectBlock:
						TriggerObjectBlock();
						break;
				}
			}
		}
	}
}