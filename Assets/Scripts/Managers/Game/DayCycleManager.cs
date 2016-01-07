﻿using UnityEngine;

namespace VoidInc
{
	public class DayCycleManager : MonoBehaviour
	{
		/// <summary>
		/// Time of day in minutes.
		/// </summary>
		public float DayLength = 1024.0f;

		/// <summary>
		/// The current time of day.
		/// </summary>
		public float CurrentTime = 0.5f;

		/// <summary>
		/// The sun's initial intensity.
		/// </summary>
		private float _SunInitialIntensity = 1.0f;

		// Update is called once per frame
		void Update()
		{
			UpdateSun();

			CurrentTime += (Time.deltaTime / (DayLength)) * 5f;

			if (CurrentTime >= 1)
			{
				CurrentTime = 0;
			}
		}

		void UpdateSun()
		{
			float intensityMultiplier = 1.0f;

			if (CurrentTime <= 0.10f || CurrentTime >= 0.80f)
			{
				intensityMultiplier = 0.25f;
			}
			else if (CurrentTime <= 0.30f)
			{
				intensityMultiplier = Mathf.Clamp((CurrentTime - 0.23f) * (1 / 0.02f), 0.25f, 1.0f);
			}
			else if (CurrentTime >= 0.60f)
			{
				intensityMultiplier = Mathf.Clamp((1 - ((CurrentTime - 0.73f) * (1 / 0.02f))), 0.25f, 1.0f);
			}

			gameObject.GetComponent<Light>().intensity = _SunInitialIntensity * intensityMultiplier;
		}
	}
}