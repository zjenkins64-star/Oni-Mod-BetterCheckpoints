namespace BetterCheckpoints
{
    public static class ModStrings
    {
        public static class Options
        {
            public const string BIONIC_HANDLING_TITLE = "Bionic Duplicants";

            public const string BIONIC_HANDLING_TOOLTIP =
                "Choose how Bionic duplicants interact with Atmo Suit and "
                + "Oxygen Mask checkpoints.\n\n"
                + "Default: treat them like Standard duplicants — they appear "
                + "in the per-checkpoint side screen, equip a suit on entry, "
                + "and drop it on return.\n\n"
                + "Bypass: ignore the checkpoint entirely — they walk through "
                + "unchanged and don't appear in the side screen.\n\n"
                + "⚠ Changing this value will restart Oxygen Not Included.";
        }

        public static class SideScreen
        {
            public const string TITLE = "Access Permissions";

            public const string DEFAULT_ROW = "Default";
            public const string USING_DEFAULT = "Default Access";
            public const string USING_CUSTOM = "Custom Access";

            public const string GROUP_STANDARD = "Standard";
            public const string GROUP_BIONIC = "Bionic";
            public const string GROUP_ROBOTS = "Robots";

            // Short labels used in the column header row. Each must fit
            // in a 28px-wide cell aligned over its checkbox; we use two
            // lines (\n) so each line stays narrow.
            public const string COLUMN_WITH_SUIT = "With\nSuit";
            public const string COLUMN_WITHOUT_SUIT = "No\nSuit";
            public const string COLUMN_RESTRICT = "Block";

            public const string WITH_SUIT_TOOLTIP = "Dupe may only pass with a suit";
            public const string WITHOUT_SUIT_TOOLTIP = "Dupe may only pass without a suit";
            public const string RESTRICT_TOOLTIP = "Check to block passage.";

            public const string USE_DEFAULT_TOOLTIP =
                "Toggle between using the group default and a custom per-duplicant setting.";
        }
    }
}
