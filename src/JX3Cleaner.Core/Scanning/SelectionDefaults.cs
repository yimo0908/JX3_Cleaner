using JX3Cleaner.Core.Config;

namespace JX3Cleaner.Core.Scanning;

/// <summary>
/// 初始勾选的唯一判据：分类声明要默认勾选，且该节点本次确实有可清理条目。
///
/// 分散在各粒度里手写会漏掉一半条件，典型翻车是「时间限制把整目录挡掉后
/// 分类仍然勾着」——界面上打着勾、底部却是「已选 0 项」，语义直接对不上。
/// </summary>
public static class SelectionDefaults
{
    public static bool Resolve(bool categoryDefault, int cleanableCount) =>
        categoryDefault && cleanableCount > 0;

    public static bool Resolve(CategoryDefinition def, int cleanableCount) =>
        Resolve(def.DefaultSelected, cleanableCount);
}
