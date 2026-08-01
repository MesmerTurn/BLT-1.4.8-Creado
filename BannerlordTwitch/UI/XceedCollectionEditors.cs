using System;
using System.Collections.Generic;
using System.Linq;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace BannerlordTwitch.UI
{
    // Drop-in replacements for DefaultCollectionEditor / DerivedClassCollectionEditor<T> that use
    // Xceed's own built-in collection editor (CollectionControlButton, compiled into
    // Xceed.Wpf.Toolkit.dll) instead of our custom CollectionPropertyEditor.xaml (compiled into
    // BannerlordTwitch.dll). Some users' WPF installations fail to resolve the pack:// URI for our
    // own compiled XAML resource ("does not have a resource identified by the URI
    // '/BannerlordTwitch;component/ui/collectionpropertyeditor.xaml'") when opening any collection
    // property in BLT Configure - routing through Xceed's own (already-proven, widely used) editor
    // resources avoids that failure mode entirely, with identical Add/Remove/Duplicate/derived-type
    // functionality. Mirrors Xceed's own PropertyGrid.Editors.CollectionEditor implementation.
    public class XceedCollectionEditor : TypeEditor<CollectionControlButton>
    {
        protected override void SetValueDependencyProperty()
        {
            ValueProperty = CollectionControlButton.ItemsSourceProperty;
        }

        protected override void ResolveValueBinding(PropertyItem propertyItem)
        {
            Editor.ItemsSourceType = propertyItem.PropertyType;
            base.ResolveValueBinding(propertyItem);
        }
    }

    public class XceedDerivedClassCollectionEditor<T> : TypeEditor<CollectionControlButton>
    {
        private static readonly Lazy<IList<Type>> derivedTypes = new(() =>
        {
            var tType = typeof(T);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => !p.IsAbstract && tType.IsAssignableFrom(p))
                .ToList();
        });

        protected override void SetValueDependencyProperty()
        {
            ValueProperty = CollectionControlButton.ItemsSourceProperty;
        }

        protected override void ResolveValueBinding(PropertyItem propertyItem)
        {
            Editor.ItemsSourceType = propertyItem.PropertyType;
            Editor.NewItemTypes = derivedTypes.Value;
            base.ResolveValueBinding(propertyItem);
        }
    }
}
