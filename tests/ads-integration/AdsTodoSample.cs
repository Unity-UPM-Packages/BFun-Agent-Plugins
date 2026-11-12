using UnityEngine;

// Test fixture for the ads-integration skill (steps 9–10).
// Copy into <test project>/Assets/Scripts/ before running the skill.
// Compiles without Ads Manager, so it can sit in the project from the start.
// Expected scan result: tests/ads-integration/EXPECTED.md

public class AdsTodoSample : MonoBehaviour
{
    // TODO ADS NativeBanner Show order=One pos=class_level
    // ^ case 13: outside a method body → invalid

    private int coins;
    private int level;

    private void Start()
    {
        // TODO ADS NativeInter Load order=One,Two
        // ^ case 1: valid

        // TODO ADS NativeReward Load order=One
        // ^ case 3: valid

        // TODO ADS NativeBanner Show order=One
        // ^ case 6: pos missing → warning, propose a pos
    }

    public void OnLevelComplete()
    {
        level++;
        // TODO ADS NativeInter Show order=One,Two pos=level_complete onClose=GoNextLevel
        // ^ case 2: valid
    }

    public void OnPlayerDied()
    {
        // TODO ADS NativeReward Show order=One pos=revive
        // ^ case 4: onClose missing (required) → invalid, agent must ask which method grants the revive
    }

    public void OnPauseClicked()
    {
        // TODO ADS NativeInter Show order=One pos=pause onClose=ResumeGame
        // ^ case 5: NativeInter Show needs exactly 2 orders → invalid
    }

    public void OnShopClicked()
    {
        // TODO ADS NativeInter Show order=One,Two pos=shop onClose=OpenShop
        // ^ case 8: OpenShop does not exist in this class → invalid
    }

    public void OnBonusClicked()
    {
        // TODO ADS NativeReward Show order=One pos=bonus onClose=AddCoins
        // ^ case 9: AddCoins takes a parameter → invalid
    }

    public void OnDoubleCoinsClicked()
    {
        // TODO ADS Rewarded Show order=One pos=double_coins onReward=GiveDoubleCoins
        // ^ case 10: valid (standard format, through AdsManager)
    }

    public void OnBackToMenu()
    {
        // TODO ADS Interstitial Show order=One pos=back_to_menu
        // ^ case 11: onClose recommended → warning
    }

    public void OnSettingsOpened()
    {
        // TODO ADS NativeInters Show order=One,Two pos=settings onClose=ResumeGame
        // ^ case 12: unknown format (typo of NativeInter) → invalid, suggest NativeInter
    }

    public void OnHomeHidden()
    {
        // TODO ADS NativeBanner Hide order=One
        // ^ case 7: valid
    }

    public void OnDailyRewardClicked()
    {
        // TODO: show reward ads here
        // ^ case 14a: near-miss, report with a suggested rewrite, do not convert
    }

    public void OnLevelFailed()
    {
        // TODO ads NativeInter show order=One,Two pos=level_failed onClose=GoNextLevel
        // ^ case 14b: near-miss (lower case "ads", "show"), report, do not convert
    }

    public void OnAlreadyDone()
    {
        // ADS: NativeBanner Hide order=One
        // ^ case 15: already converted in an earlier run → skip, not listed
    }

    // ─── Callbacks ───────────────────────────────────────────────

    private void GoNextLevel()
    {
        Debug.Log("[AdsTodoSample] GoNextLevel " + level);
    }

    private void ResumeGame()
    {
        Debug.Log("[AdsTodoSample] ResumeGame");
    }

    private void GrantRevive()
    {
        Debug.Log("[AdsTodoSample] GrantRevive");
    }

    private void GiveDoubleCoins()
    {
        coins *= 2;
        Debug.Log("[AdsTodoSample] GiveDoubleCoins " + coins);
    }

    private void AddCoins(int amount)
    {
        coins += amount;
    }
}
