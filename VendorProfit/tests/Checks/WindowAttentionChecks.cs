using VendorProfit;

static class WindowAttentionChecks
{
    public static void Run(Action<bool, string> check)
    {
        int flashes = 0;
        void Flash() => flashes++;
        check(!WindowAttention.FlashIfInactive(42, () => 42, Flash) && flashes == 0,
            "attention: active game, including test notifications, never invokes flashing");
        check(WindowAttention.FlashIfInactive(42, () => 99, Flash) && flashes == 1,
            "attention: another foreground process allows a single flash request");
        check(!WindowAttention.FlashIfInactive(42, () => null, Flash) && flashes == 1,
            "attention: missing foreground window does not trigger flashing");
        check(!WindowAttention.FlashIfInactive(42, () => 0, Flash) && flashes == 1,
            "attention: failed foreground process lookup does not trigger flashing");
        check(!WindowAttention.FlashIfInactive(0, () => 99, Flash) && flashes == 1,
            "attention: unknown game process does not trigger flashing");
        check(!WindowAttention.FlashIfInactive(42, () => 42, Flash) && flashes == 1,
            "attention: returning to the game suppresses subsequent requests");
    }
}
