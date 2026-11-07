using System;
using System.Linq;
using UnityEngine;

namespace WorldChronicle
{
    internal static class RealmArt
    {
        // Small historical copies of the native banner, retained when a realm vanishes.
        internal static void Observe(ChronicleJournal journal, AnnualObservation observation)
        {
            if (journal.Illustrations == null) journal.Illustrations = new System.Collections.Generic.List<RealmIllustration>();
            foreach (var nation in observation.Nations.OrderByDescending(n => n.Territory).ThenBy(n => n.Id).Take(3))
            {
                try
                {
                    long id;
                    Kingdom kingdom;
                    if (!long.TryParse(nation.Id, out id) || !World.world.kingdoms.dict.TryGetValue(id, out kingdom)) continue;
                    var library = AssetManager.kingdom_banners_library;
                    var background = library.getSpriteBackground(kingdom.data.banner_background_id);
                    var icon = library.getSpriteIcon(kingdom.data.banner_icon_id);
                    string png = Capture(background, icon, kingdom.getColor().getColorBanner(), kingdom.getColor().getColorMain());
                    int century = observation.Year / 100 + 1;
                    var entry = journal.Illustrations.FirstOrDefault(x => x.CenturyIndex == century && x.NationId == nation.Id);
                    if (entry == null)
                    {
                        entry = new RealmIllustration { CenturyIndex = century, NationId = nation.Id };
                        journal.Illustrations.Add(entry);
                    }
                    entry.Year = observation.Year;
                    entry.NationName = nation.Name;
                    entry.PngBase64 = png;
                }
                catch (Exception ex) { Debug.LogWarning("[WorldChronicle] Brasão indisponível: " + ex.Message); }
            }
        }

        private static string Capture(Sprite background, Sprite icon, Color backColor, Color iconColor)
        {
            if (background == null || icon == null) throw new InvalidOperationException("Imagem nativa ausente.");
            RenderTexture previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(96, 96, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = null;
            bool pushed = false;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix(); pushed = true;
                GL.LoadPixelMatrix(0, 96, 96, 0);
                Draw(background, backColor);
                Draw(icon, iconColor);
                copy = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                copy.Apply();
                return Convert.ToBase64String(copy.EncodeToPNG());
            }
            finally
            {
                if (pushed) GL.PopMatrix();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        private static void Draw(Sprite sprite, Color color)
        {
            var rect = sprite.textureRect;
            var texture = sprite.texture;
            var uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
            Graphics.DrawTexture(new Rect(8, 8, 80, 80), texture, uv, 0, 0, 0, 0, color);
        }
    }
}
