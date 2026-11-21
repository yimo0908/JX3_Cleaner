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

    // ---------------- 保留行不可勾（云团监「保留最近 M 组」） ----------------

    /// <summary>云团监分类：前 retained 组被保留策略护住（无条目），其余组可清理。</summary>
    private static ScanNode TeamMonCategory(int cleanable, int retained)
    {
        var category = new ScanNode
        {
            Kind = NodeKind.Category,
            Category = CleanCategory.CloudTeamMon,
            Title = "云团监数据",
            Selectable = true
        };

        for (var i = 0; i < retained; i++)
            category.Add(new ScanNode
            {
                Kind = NodeKind.Group,
                Category = CleanCategory.CloudTeamMon,
                Title = $"r-keep{i}",
                Flags = NodeFlags.Retained,
                SizeBytes = 100,
                FileCount = 2
            });

        for (var i = 0; i < cleanable; i++)
        {
            var node = new ScanNode
            {
                Kind = NodeKind.Group,
                Category = CleanCategory.CloudTeamMon,
                Title = $"r-old{i}",
                SizeBytes = 100,
                FileCount = 2
            };

            node.Targets.Add(new TargetItem(
                Path.Combine(Path.GetTempPath(), "jx3-vm", $"r-old{i}.jx3dat"), 100, DateTime.UtcNow, false));
            category.Add(node);
        }

        return category;
    }

    [Fact]
    public void 保留行自己点也勾不上()
    {
        var vm = new TreeItemVm(TeamMonCategory(2, 3), null, null, expand: false);
        var retained = vm.Children.Where(c => c.IsRetained).ToList();

        Assert.Equal(3, retained.Count);
        Assert.All(retained, r => Assert.False(r.IsCheckable));

        retained[0].IsChecked = true;

        Assert.False(retained[0].IsChecked);
        Assert.False(retained[0].CheckedExplicitly);
    }

    [Fact]
    public void 全选分类时跳过保留行()
    {
        var vm = new TreeItemVm(TeamMonCategory(2, 3), null, null, expand: false);

        vm.IsChecked = true;                     // 相当于「全选」勾了云团监数据这一分类

        Assert.True(vm.IsChecked);
        Assert.All(vm.Children.Where(c => !c.IsRetained), c => Assert.True(c.IsChecked));
        Assert.All(vm.Children.Where(c => c.IsRetained), c => Assert.False(c.IsChecked));
    }

    [Fact]
    public void 三态只统计可勾子项_保留行不会把分类拖成半选()
    {
        var vm = new TreeItemVm(TeamMonCategory(2, 3), null, null, expand: false);

        foreach (var child in vm.Children.Where(c => c.IsCheckable))
            child.IsChecked = true;

        Assert.True(vm.IsChecked);               // 3 个保留行没勾，也不该变三态
    }
}
