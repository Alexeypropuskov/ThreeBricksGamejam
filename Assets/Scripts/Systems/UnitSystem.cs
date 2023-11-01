﻿using System;
using Behaviours;
using JetBrains.Annotations;
using Netologia.Behaviours;
using Netologia.TowerDefence;
using Netologia.TowerDefence.Behaviors;
using Netologia.TowerDefence.Settings;
using UnityEngine;
using Zenject;

namespace Netologia.Systems
{
	public class UnitSystem : GameObjectPoolContainer<Unit>, Director.IManualUpdate
	{
		private Director _director;					//injected
		private EffectSystem _effects;				//injected
		private Constants _constants;				//injected
		private Vector3[] _path;					//injected

		[SerializeField, Min(0.01f)]
		private float _arrivalDistance = 0.1f;

		public event Action<int> OnDespawnUnitHandler;

		[CanBeNull]
		public Unit FindTarget(in Vector3 position, float range)
		{
			range *= range;
			var target = default(Unit);
			foreach (var pair in this)
			{
				foreach (var unit in pair)
				{
					var distance = Vector3.SqrMagnitude(unit.transform.position - position);
					if (distance < range)
						(range, target) = (distance, unit);
				}
			}

			return target;
		}

		public void ManualUpdate()
		{
			var delta = TimeManager.DeltaTime;
			var time = TimeManager.Time;
			foreach (var pair in this)
			{
				foreach (var unit in pair)
				{
					var position = unit.transform.position;
					//Dead unit
					if (unit.CurrentHealth <= 0f)
					{
						OnDespawnUnitHandler?.Invoke(unit.ID);
						DespawnUnit(unit, in position);
						continue;
					}

					//UpdateVisual
					unit.Visual.ManualUpdate(delta);
					//Update effects
					unit.CurrentHealth -= unit.Stats.Health * unit.CountEffect(ElementalType.Fire) * _constants.FireDebuffDamageMult;
					unit.TryRemoveEffect(time, ElementalType.Fire);
					unit.TryRemoveEffect(time, ElementalType.Ice);
					
					//Move to finish
					var point = _path[unit.PathIndex];
					unit.transform.position = position += Vector3.Normalize(point - position) * (unit.MoveSpeed * delta);
					//Update path
					if (Vector3.SqrMagnitude(position - point) <= _arrivalDistance)
					{
						unit.PathIndex++;
						//Finish
						if (_path.Length <= unit.PathIndex)
						{
							_director.AddPlayerDamage(1);//if all enemies takes one damage
							this[unit.Ref].ReturnElement(unit.ID);
						}
					}
				}
			}
		}

		private void DespawnUnit(Unit unit, in Vector3 position)
		{
			//Create HitEffect
			if (unit.HasEffect)
			{
				var effect = _effects[unit.DieEffect].Get;
				effect.transform.position = position;
				effect.Play();
			}
			//Play sound
			if (unit.HasSound)
			{
				AudioManager.PlayHit(unit.DieSound);
			}

			_director.AddMoney(unit.Stats.Cost);
			this[unit.Ref].ReturnElement(unit.ID);
		}
		
		[Inject]
		private void Construct(EffectSystem effects, Director director, Constants constants, WaveController path)
		{
			(_effects, _director, _constants, _path) = (effects, director, constants, path.GetPath());
			_arrivalDistance *= _arrivalDistance;
			AwakeMethod = t => t.Constants = _constants;
		}
	}
}