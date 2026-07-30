using Avalonia.Controls;
using Avalonia.VisualTree;

namespace DarkBlendTheme
{
    public static class TreeViewItemExtensions
    {
        public static int GetDepth(this TreeViewItem item)
        {
            TreeViewItem parent;
            while ((parent = GetParent(item)) != null) return GetDepth(parent) + 1;
            return 0;
        }

        private static TreeViewItem GetParent(TreeViewItem item)
        {
            var parent = item.GetVisualParent();

            while (!(parent is TreeViewItem || parent is TreeView))
            {
                if (parent == null) return null;
                parent = parent.GetVisualParent();
            }

            return parent as TreeViewItem;
        }
    }
}
