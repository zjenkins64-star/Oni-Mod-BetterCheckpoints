using HarmonyLib;

public class BetterCheckpointsPatches
{
	public const string MOD_NAME = "BetterCheckpoints";
	public const string MOD_VERSION = "1.0.0";

	public static void OnLoad(Harmony harmony)
	{
		harmony.PatchAll();
	}
}
