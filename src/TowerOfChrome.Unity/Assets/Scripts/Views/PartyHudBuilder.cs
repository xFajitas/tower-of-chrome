using TowerOfChrome.Core.Entities;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>
    /// Renders the party status panel (name/level/class, HP/MP bars, K.O./status tags) into any
    /// container VisualElement. Shared across Explore/Combat/Inventory, matching Python's
    /// BaseScreen._draw_party_hud helper used identically by all three screens there.
    /// </summary>
    public static class PartyHudBuilder
    {
        public static void Render(VisualElement container, Party party)
        {
            container.Clear();
            container.AddToClassList("hud-panel");

            foreach (var member in party.AllMembers)
            {
                var row = new VisualElement();
                row.AddToClassList("hud-row");

                var header = new VisualElement();
                header.AddToClassList("hud-row-header");

                var avatar = new VisualElement();
                avatar.AddToClassList("hud-avatar");
                var avatarTex = ArchetypeIcons.ForRole(member.ClassDef.Role);
                if (avatarTex != null)
                    avatar.style.backgroundImage = new StyleBackground(avatarTex);
                header.Add(avatar);

                var textCol = new VisualElement();
                textCol.AddToClassList("hud-text-col");

                var nameLabel = new Label($"{member.Name}  Lv.{member.Level}");
                nameLabel.AddToClassList("hud-name");
                if (member.IsKo)
                    nameLabel.AddToClassList("hud-name--dead");
                textCol.Add(nameLabel);

                var classLabel = new Label(member.ClassDef.Name);
                classLabel.AddToClassList("hud-class");
                textCol.Add(classLabel);

                header.Add(textCol);
                row.Add(header);

                row.Add(BuildBar(member.HpFraction, "hud-bar-fill--hp", $"{member.CurrentHp}/{member.MaxHp}"));
                row.Add(BuildBar(member.MpFraction, "hud-bar-fill--mp", $"{member.CurrentMp}/{member.MaxMp}"));

                if (member.IsKo)
                {
                    var koLabel = new Label("K.O.");
                    koLabel.AddToClassList("hud-status-tag");
                    koLabel.style.color = new UnityEngine.Color(0.8f, 0.2f, 0.2f);
                    var statusRow = new VisualElement();
                    statusRow.AddToClassList("hud-status-row");
                    statusRow.Add(koLabel);
                    row.Add(statusRow);
                }
                else if (member.StatusEffects.Count > 0)
                {
                    var statusRow = new VisualElement();
                    statusRow.AddToClassList("hud-status-row");
                    foreach (var effect in member.StatusEffects)
                    {
                        var tag = new Label(effect.Length >= 3 ? effect.Substring(0, 3).ToUpperInvariant() : effect.ToUpperInvariant());
                        tag.AddToClassList("hud-status-tag");
                        statusRow.Add(tag);
                    }
                    row.Add(statusRow);
                }

                container.Add(row);
            }
        }

        private static VisualElement BuildBar(double fraction, string fillClass, string text)
        {
            var bg = new VisualElement();
            bg.AddToClassList("hud-bar-bg");

            var fill = new VisualElement();
            fill.AddToClassList(fillClass);
            fill.style.width = new StyleLength(Length.Percent((float)(System.Math.Clamp(fraction, 0.0, 1.0) * 100.0)));
            bg.Add(fill);

            var label = new Label(text);
            label.AddToClassList("hud-bar-text");
            bg.Add(label);

            return bg;
        }
    }
}
