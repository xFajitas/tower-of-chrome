using System.Collections;
using NUnit.Framework;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Exercises InventoryScreenView by calling its public navigate/action methods
/// directly. Uses the real default party (knight/mage/cleric/ranger with class data from
/// StreamingAssets) since equip/trade fidelity depends on real item and class definitions
/// (e.g. the knight's starting iron_sword, +4 str/+1 dex).</summary>
public class InventoryScreenTests
{
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null;
        onLoaded(gm);
    }

    private static InventoryScreenView GetInventoryView(GameManager gm) =>
        gm.GetScreenRoot(GameState.Inventory).GetComponent<InventoryScreenView>();

    private static IEnumerator EnterInventory(GameManager gm)
    {
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;
        gm.SwitchTo(GameState.Inventory);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OnEnable_ShowsMemberSelect_WithDefaultParty()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);

        Assert.AreEqual(InventoryUiState.MemberSelect, view.State);
        Assert.AreEqual(0, view.MemberSelected);
    }

    [UnityTest]
    public IEnumerator NavigateMember_WrapsAroundInBothDirections()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var n = gm.Party.AllMembers.Count;

        view.NavigateMemberUp(); // wraps from 0 to n-1
        Assert.AreEqual(n - 1, view.MemberSelected);

        for (var i = 0; i < n; i++)
            view.NavigateMemberDown();
        Assert.AreEqual(n - 1, view.MemberSelected);
    }

    [UnityTest]
    public IEnumerator ConfirmMember_EntersItemSelect()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);

        view.ConfirmMember();

        Assert.AreEqual(InventoryUiState.ItemSelect, view.State);
        Assert.AreEqual(0, view.ItemCursor);
    }

    [UnityTest]
    public IEnumerator CancelItems_ReturnsToMemberSelect()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        view.ConfirmMember();

        view.CancelItems();

        Assert.AreEqual(InventoryUiState.MemberSelect, view.State);
    }

    [UnityTest]
    public IEnumerator UnequipSelected_MovesWeaponToBag_AndReducesStrength()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        // Slot 0 is the knight (Sir Gareth), starting equipped with iron_sword (+4 str/+1 dex).
        var knight = gm.Party.AllMembers[0];
        var strBefore = knight.Strength;
        view.ConfirmMember(); // cursor 0 == weapon slot (first in SlotOrder)

        view.UnequipSelected();

        Assert.IsNull(knight.Equipment["weapon"]);
        CollectionAssert.Contains(knight.Inventory.Bag, "iron_sword");
        Assert.AreEqual(strBefore - 4, knight.Strength);
    }

    [UnityTest]
    public IEnumerator ActivateItem_EquipsBagItem_AndRestoresStrength()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var knight = gm.Party.AllMembers[0];
        var strBefore = knight.Strength;
        view.ConfirmMember();
        view.UnequipSelected(); // iron_sword now in bag at row index 4 (4 equip slots precede it)

        for (var i = 0; i < 4; i++)
            view.NavigateItemDown();
        view.ActivateItem();

        Assert.AreEqual("iron_sword", knight.Equipment["weapon"]);
        CollectionAssert.DoesNotContain(knight.Inventory.Bag, "iron_sword");
        Assert.AreEqual(strBefore, knight.Strength);
    }

    [UnityTest]
    public IEnumerator ActivateItem_UsesConsumable_RestoresHp_AndRemovesFromBag()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var knight = gm.Party.AllMembers[0];
        knight.TakeDamage(40);
        knight.Inventory.Add("health_potion_small"); // heal_hp 50
        var hpBefore = knight.CurrentHp;
        view.ConfirmMember();
        for (var i = 0; i < 4; i++) // skip the 4 equip-slot rows to reach the bag row
            view.NavigateItemDown();

        view.ActivateItem();

        Assert.AreEqual(hpBefore + 40, knight.CurrentHp); // clamped heal: only 40 HP was missing
        CollectionAssert.DoesNotContain(knight.Inventory.Bag, "health_potion_small");
    }

    [UnityTest]
    public IEnumerator DropSelected_RemovesItemFromBag()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var knight = gm.Party.AllMembers[0];
        knight.Inventory.Add("health_potion_small");
        view.ConfirmMember();
        for (var i = 0; i < 4; i++)
            view.NavigateItemDown();

        view.DropSelected();

        CollectionAssert.DoesNotContain(knight.Inventory.Bag, "health_potion_small");
    }

    [UnityTest]
    public IEnumerator BeginTrade_ThenConfirm_MovesItemToRecipient()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var giver = gm.Party.AllMembers[0];
        var receiver = gm.Party.AllMembers[1];
        giver.Inventory.Add("health_potion_small");
        view.ConfirmMember();
        for (var i = 0; i < 4; i++)
            view.NavigateItemDown();

        view.BeginTrade();
        Assert.AreEqual(InventoryUiState.TradeTarget, view.State);
        Assert.AreEqual(1, view.TradeTargetSlot); // only other member at this point is slot 1

        view.ConfirmTrade();

        Assert.AreEqual(InventoryUiState.ItemSelect, view.State);
        CollectionAssert.DoesNotContain(giver.Inventory.Bag, "health_potion_small");
        CollectionAssert.Contains(receiver.Inventory.Bag, "health_potion_small");
    }

    [UnityTest]
    public IEnumerator ConfirmTrade_RejectsWhenReceiverBagFull()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);
        var giver = gm.Party.AllMembers[0];
        var receiver = gm.Party.AllMembers[1];
        giver.Inventory.Add("health_potion_small");
        for (var i = 0; i < 20; i++)
            receiver.Inventory.Add("health_potion_small");
        view.ConfirmMember();
        for (var i = 0; i < 4; i++)
            view.NavigateItemDown();
        view.BeginTrade();

        view.ConfirmTrade();

        CollectionAssert.Contains(giver.Inventory.Bag, "health_potion_small");
        StringAssert.Contains("bag is full", view.Message);
    }

    [UnityTest]
    public IEnumerator Back_FromMemberSelect_ReturnsToExplore()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return EnterInventory(gm);
        var view = GetInventoryView(gm);

        view.Back();

        Assert.AreEqual(GameState.Explore, gm.Fsm.State);
    }
}
