using JX3Cleaner.App.ViewModels;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Tests;

/// <summary>
/// 勾选状态机：三态父子联动 + 「这次勾选是谁点出来的」。
///
/// 这套逻辑是旧区服误删的现场：勾一个过期大区会把账号行推成「全选」，
/// 如果只按 IsChecked 收集条目，账号行的整包删除条目就会混进计划。
/// </summary>
public sealed class TreeItemVmTests
{
    private static TargetItem Dir(string path, long size) =>
        new(path, size, DateTime.UtcNow, IsDirectory: true);

    /// <summary>账号行（整包删除目标）+ 若干过期大区子节点。</summary>
    private static ScanNode AccountWithLegacy(params string[] regions)
    {
        var accountDir = Path.Combine(Path.GetTempPath(), "jx3-vm", "acc");

        var account = new ScanNode
        {
            Kind = NodeKind.Account,
            Title = "acc",
            FullPath = accountDir,
            Flags = NodeFlags.HasLegacyChild,
            SizeBytes = 1500,
            Targets = { Dir(accountDir, 1000) }
        };

        foreach (var region in regions.Length == 0 ? ["电信五区"] : regions)
        {
            var regionDir = Path.Combine(accountDir, region);

            account.Add(new ScanNode
            {
                Kind = NodeKind.Region,
                Title = region,
                FullPath = regionDir,
                Flags = NodeFlags.LegacyRegion,
                SizeBytes = 500,
                Targets = { Dir(regionDir, 500) }
            });
        }

        return account;
    }

    [Fact]
    public void 勾选子节点会把父节点推成全选_但那不算用户勾了父节点()
    {
        var vm = new TreeItemVm(AccountWithLegacy(), null, null, expand: false);
        var legacy = Assert.Single(vm.Children);

        legacy.IsChecked = true;

        Assert.True(vm.IsChecked);
        Assert.False(vm.CheckedExplicitly);      // 派生状态，不能代表「删整个账号」
        Assert.False(vm.InvolvesLegacy);         // 因此不触发整账号级别的过期区服确认
        Assert.True(legacy.InvolvesLegacy);      // 但大区行本身要确认
    }

    [Fact]
    public void 直接勾选账号行才是整包删除意图()
    {
        var vm = new TreeItemVm(AccountWithLegacy(), null, null, expand: false);

        vm.IsChecked = true;

        Assert.True(vm.CheckedExplicitly);
        Assert.True(vm.InvolvesLegacy);
        Assert.All(vm.Children, c => Assert.True(c.IsChecked));
    }

    [Fact]
    public void 回滚勾选会清掉直接勾选标记()
    {
        var vm = new TreeItemVm(AccountWithLegacy(), null, null, expand: false);

        vm.IsChecked = true;
        vm.RevertCheck(false);

        Assert.False(vm.IsChecked);
        Assert.False(vm.CheckedExplicitly);
    }

    [Fact]
    public void 取消一个子节点后父节点变为三态_再次勾上也不再是直接勾选()
    {
        var vm = new TreeItemVm(AccountWithLegacy("电信五区", "电信六区"), null, null, expand: false);

        vm.IsChecked = true;                     // 直接勾了账号行
        vm.Children[0].IsChecked = false;        // 反悔，去掉其中一个旧大区

        Assert.Null(vm.IsChecked);               // 三态：不参与清理
        Assert.False(vm.CheckedExplicitly);

        vm.Children[0].IsChecked = true;         // 再勾回来

        Assert.True(vm.IsChecked);
        Assert.False(vm.CheckedExplicitly);      // 这次是推导出来的，不能升级成整包删除
    }

    [Fact]
    public void 有子节点时状态由子项推导_分类默认值不再生效()
    {
        var node = AccountWithLegacy();
        node.DefaultSelected = true;             // 子节点都是默认不勾

        var vm = new TreeItemVm(node, null, null, expand: false);

        Assert.False(vm.IsChecked);
        Assert.False(vm.CheckedExplicitly);
    }

    [Fact]
    public void 可见性默认打开且与勾选互不影响()
    {
        var vm = new TreeItemVm(AccountWithLegacy(), null, null, expand: false);

        Assert.True(vm.IsVisible);

        vm.IsVisible = false;
        vm.IsChecked = true;

        Assert.False(vm.IsVisible);
        Assert.True(vm.IsChecked);
    }
}
