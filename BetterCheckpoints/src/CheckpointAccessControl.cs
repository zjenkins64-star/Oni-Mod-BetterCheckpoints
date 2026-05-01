using System;
using System.Collections.Generic;
using KSerialization;
using UnityEngine;

[SerializationConfig(MemberSerialization.OptIn)]
[AddComponentMenu("KMonoBehaviour/scripts/CheckpointAccessControl")]
public class CheckpointAccessControl : KMonoBehaviour
{
	[System.Serializable]
	public struct DupeRule
	{
		public int dupeId;
		public bool canPassWithSuit;
		public bool canPassWithoutSuit;
		public AccessControl.Permission direction;
	}

	[Serialize]
	private List<DupeRule> rules = new List<DupeRule>();

	[Serialize]
	public bool controlEnabled = true;

	public bool CanPassWithSuit(int dupeId)
	{
		foreach (var rule in rules)
		{
			if (rule.dupeId == dupeId)
				return rule.canPassWithSuit;
		}
		return true;
	}

	public bool CanPassWithoutSuit(int dupeId)
	{
		foreach (var rule in rules)
		{
			if (rule.dupeId == dupeId)
				return rule.canPassWithoutSuit;
		}
		return true;
	}

	public AccessControl.Permission GetDirection(int dupeId)
	{
		foreach (var rule in rules)
		{
			if (rule.dupeId == dupeId)
				return rule.direction;
		}
		return AccessControl.Permission.Both;
	}

	public void SetRule(int dupeId, bool withSuit, bool withoutSuit, AccessControl.Permission direction)
	{
		for (int i = 0; i < rules.Count; i++)
		{
			if (rules[i].dupeId == dupeId)
			{
				rules[i] = new DupeRule { dupeId = dupeId, canPassWithSuit = withSuit, canPassWithoutSuit = withoutSuit, direction = direction };
				return;
			}
		}
		rules.Add(new DupeRule { dupeId = dupeId, canPassWithSuit = withSuit, canPassWithoutSuit = withoutSuit, direction = direction });
	}

	public void ClearRule(int dupeId)
	{
		for (int i = rules.Count - 1; i >= 0; i--)
		{
			if (rules[i].dupeId == dupeId)
			{
				rules.RemoveAt(i);
				break;
			}
		}
	}
}
