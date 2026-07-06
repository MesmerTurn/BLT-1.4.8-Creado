using BannerlordTwitch.Localization;
using JetBrains.Annotations;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BannerlordTwitch
{
    [LocDescription("{=8SbGSWFY}Bot command definition")]
    public class Command : ActionBase
    {
        [LocDisplayName("{=D1lt1bSQ}Name"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=u1Ua0LXJ}The command itself, not including the !"),
         PropertyOrder(1), UsedImplicitly]
        public LocString Name { get; set; } = string.Empty;
        [LocDisplayName("{=TpnV5Mpi}HideHelp"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=0AZVaBQN}Hides the command from the !help action"),
         PropertyOrder(2), UsedImplicitly]
        public bool HideHelp { get; set; }
        [LocDisplayName("{=JqNXbyIT}Help"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=9QV7UzFh}What to show in the !help command"),
         PropertyOrder(3), UsedImplicitly]
        public LocString Help { get; set; } = string.Empty;
        [LocDisplayName("{=Rh3cTAVq}Moderator only"), LocCategory("Permissions", "{=kJbOqjr1}Permissions"),
        LocDescription("{=6ggxMun9}Only moderators and broadcaster can use this command"),
        PropertyOrder(4), UsedImplicitly]
        public bool ModeratorOnly { get; set; }

        [LocDisplayName("{=BLT_CmdGroup}Group"), LocCategory("General", "{=C5T5nnix}General"),
         LocDescription("{=BLT_CmdGroupDesc}Optional: commands sharing the same Group name are shown together under their own heading in the config tool's action list, instead of the generic Chat Commands bucket. Leave blank for normal behavior."),
         PropertyOrder(5), UsedImplicitly]
        public string Group { get; set; } = string.Empty;

        [ItemsSource(typeof(CommandHandlerItemsSource))]
        public override string Handler { get; set; }

        public override string ToString() => $"{Name} ({Handler})";
    }
}