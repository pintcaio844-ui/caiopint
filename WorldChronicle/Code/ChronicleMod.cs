using System;
using HarmonyLib;
using NeoModLoader.api;
using UnityEngine;

namespace WorldChronicle
{
    public sealed class ChronicleMod : BasicMod<ChronicleMod>
    {
        private Harmony _harmony;
        private bool _visible, _showIndex, _showRecords;
        private Rect _window = new Rect(40, 60, 860, 680);
        private Vector2 _scroll;
        private int _selectedPage = -1, _seenPageCount;
        private string _lastExport, _bookBranch;
        private GUIStyle _cover, _paper, _body, _heading, _small, _coverText;
        private Texture2D _paperTexture, _coverTexture;
        private Font _bookFont;
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> _art = new System.Collections.Generic.Dictionary<string, Texture2D>();

        internal static bool OverlayBlocksPointer
        {
            get
            {
                if (Instance == null || !Instance._visible) return false;
                var mouse = Input.mousePosition;
                return Instance._window.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            }
        }

        protected override void OnModLoad()
        {
            _harmony = new Harmony("worldchronicle.cronistadomundo.v1");
            GameHooks.Install(_harmony, message => Debug.Log("[WorldChronicle] " + message));
            Debug.Log("[WorldChronicle] Livro do Mundo 0.3.0. Ctrl+Shift+H abre o livro; uma página por século.");
        }

        private void Update()
        {
            GameHooks.Pump();
            ChronicleRuntime.TickBook();
            var journal = ChronicleRuntime.Journal;
            string branch = journal == null ? null : journal.BranchId;
            if (_bookBranch != branch)
            {
                _lastExport = null;
                _bookBranch = branch;
                _selectedPage = -1;
                _seenPageCount = 0;
                _showIndex = false;
                _showRecords = false;
                _scroll = Vector2.zero;
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.H))
                _visible = !_visible;
            if (_visible && Input.GetKeyDown(KeyCode.Escape)) _visible = false;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();
            _window.width = Math.Min(920, Math.Max(300, Screen.width - 40));
            _window.height = Math.Min(760, Math.Max(260, Screen.height - 60));
            _window.x = Math.Max(0, Math.Min(_window.x, Screen.width - _window.width));
            _window.y = Math.Max(0, Math.Min(_window.y, Screen.height - _window.height));
            _window = GUILayout.Window(19420711, _window, DrawWindow, "LIVRO DO MUNDO", _cover);
        }

        private void DrawWindow(int windowId)
        {
            var journal = ChronicleRuntime.Journal;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fechar", GUILayout.Width(65))) _visible = false;
            if (journal != null && GUILayout.Button(_showIndex ? "Voltar à página" : "Índice", GUILayout.Width(125)))
            { _showIndex = !_showIndex; _scroll = Vector2.zero; }
            if (journal != null && GUILayout.Button("Exportar livro", GUILayout.Width(130)))
            {
                try { _lastExport = ChronicleRuntime.Export(); }
                catch (Exception ex) { ChronicleRuntime.Status = "Falha ao exportar: " + ex.Message; }
            }
            if (!String.IsNullOrEmpty(_lastExport) && GUILayout.Button("Copiar pasta", GUILayout.Width(110)))
                GUIUtility.systemCopyBuffer = _lastExport;
            GUILayout.EndHorizontal();
            GUILayout.Label(journal == null ? "Um livro para cada mundo" : journal.WorldName, _coverText);

            GUILayout.BeginVertical(_paper, GUILayout.ExpandHeight(true));
            var pages = ChronicleRuntime.BookPages;
            if (journal == null || pages == null || pages.Count == 0)
            {
                GUILayout.Label("As primeiras páginas", _heading);
                GUILayout.Label(ChronicleRuntime.Status, _body);
                GUILayout.Label("Carregue ou crie um mundo. Seus acontecimentos formarão uma história, com uma página para cada século.", _body);
                GUILayout.FlexibleSpace();
            }
            else
            {
                if (_selectedPage < 0 || (_selectedPage == _seenPageCount - 1 && pages.Count > _seenPageCount))
                    _selectedPage = pages.Count - 1;
                _selectedPage = Math.Max(0, Math.Min(_selectedPage, pages.Count - 1));
                _seenPageCount = pages.Count;
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                if (_showIndex)
                {
                    GUILayout.Label("Índice dos séculos", _heading);
                    for (int i = 0; i < pages.Count; i++)
                    {
                        var chapter = pages[i];
                        if (GUILayout.Button("Século " + chapter.CenturyIndex + " · " + chapter.StartYear + "–" + chapter.EndYear
                            + "   " + (chapter.IsComplete ? "Concluído" : "Em escrita")))
                        { SelectPage(i); _showIndex = false; }
                    }
                }
                else
                {
                    var page = pages[_selectedPage];
                    GUILayout.Label("SÉCULO " + Roman(page.CenturyIndex) + "    ·    ANOS " + page.StartYear + "–" + page.EndYear, _small);
                    GUILayout.Space(8);
                    GUILayout.Label(page.Title, _heading);
                    GUILayout.Label(page.IsComplete ? "Capítulo concluído" : "Em escrita · observado até o ano " + page.ObservedThroughYear, _small);
                    GUILayout.Space(14);
                    DrawIllustrations(journal, page);
                    GUILayout.Label(page.Summary, _body);
                    GUILayout.Space(14);
                    if (!String.IsNullOrEmpty(page.DominantNationName))
                        GUILayout.Label("Destaque histórico: " + page.DominantNationName, _heading);
                    if (GUILayout.Button(_showRecords ? "Recolher acontecimentos e nomes" : "Consultar acontecimentos e nomes deste século"))
                        _showRecords = !_showRecords;
                    if (_showRecords && !String.IsNullOrEmpty(page.RecordText))
                        GUILayout.Label(page.RecordText, _body);
                    GUILayout.Space(10);
                    GUILayout.Label("A influência das nações considera sua participação no território, na população e no exército ao longo dos anos observados. "
                        + "As páginas preservam os nomes usados em cada acontecimento.", _small);
                }
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal();
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && _selectedPage > 0;
                if (GUILayout.Button("< Século anterior", GUILayout.Width(150))) SelectPage(_selectedPage - 1);
                GUI.enabled = wasEnabled;
                GUILayout.FlexibleSpace();
                GUILayout.Label("Página " + (_selectedPage + 1) + " de " + pages.Count, _small);
                GUILayout.FlexibleSpace();
                GUI.enabled = wasEnabled && _selectedPage < pages.Count - 1;
                if (GUILayout.Button("Próximo século >", GUILayout.Width(150))) SelectPage(_selectedPage + 1);
                GUI.enabled = wasEnabled;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Label(ChronicleRuntime.SaveStatus, _coverText);
            GUI.DragWindow(new Rect(0, 0, _window.width, 22));
        }

        private void SelectPage(int page)
        {
            _selectedPage = page;
            _showRecords = false;
            _scroll = Vector2.zero;
        }

        private void EnsureStyles()
        {
            if (_cover != null) return;
            _paperTexture = Manuscript(false);
            _coverTexture = Manuscript(true);
            _cover = new GUIStyle(GUI.skin.window) { padding = new RectOffset(22, 22, 30, 16), richText = false };
            _cover.normal.background = _coverTexture;
            _cover.onNormal.background = _coverTexture;
            _cover.normal.textColor = new Color(0.93f, 0.82f, 0.57f);
            _paper = new GUIStyle(GUI.skin.box) { padding = new RectOffset(28, 28, 24, 18) };
            _paper.normal.background = _paperTexture;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = false, padding = new RectOffset(0, 8, 3, 10) };
            try
            {
                _bookFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Palatino Linotype", "Times New Roman", "Liberation Serif" }, 18);
                if (_bookFont != null) _body.font = _bookFont;
            }
            catch (Exception) { /* Keep the game's font if no serif font is available. */ }
            _body.normal.textColor = new Color(0.19f, 0.14f, 0.10f);
            _heading = new GUIStyle(_body) { fontSize = 25, fontStyle = FontStyle.Bold };
            _heading.normal.textColor = new Color(0.38f, 0.12f, 0.08f);
            _small = new GUIStyle(_body) { fontSize = 12 };
            _small.normal.textColor = new Color(0.39f, 0.30f, 0.20f);
            _coverText = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = false };
            _coverText.normal.textColor = new Color(0.88f, 0.79f, 0.61f);
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void DrawIllustrations(ChronicleJournal journal, CenturyPage page)
        {
            var images = BookIllustrations.ForPage(journal, page);
            if (images.Count == 0) return;
            if (_art.Count > 6) ClearArt();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            foreach (var image in images)
            {
                Texture2D texture;
                if (!_art.TryGetValue(image.PngBase64, out texture))
                {
                    texture = new Texture2D(2, 2);
                    if (!texture.LoadImage(Convert.FromBase64String(image.PngBase64))) { Destroy(texture); continue; }
                    texture.filterMode = FilterMode.Point;
                    _art.Add(image.PngBase64, texture);
                }
                GUILayout.BeginVertical(GUILayout.Width(130));
                GUILayout.Label(texture, GUILayout.Width(96), GUILayout.Height(96));
                GUILayout.Label(image.NationName, _small);
                GUILayout.Label("Ano " + image.Year, _small);
                GUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
        }

        private static Texture2D Manuscript(bool leather)
        {
            const int size = 256;
            var texture = new Texture2D(size, size);
            var pixels = new Color[size * size];
            var random = new System.Random(leather ? 47 : 19);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int edge = Math.Min(Math.Min(x, size - 1 - x), Math.Min(y, size - 1 - y));
                float grain = (float)random.NextDouble() * (leather ? 0.025f : 0.035f);
                float wear = Math.Max(0, 1f - edge / 24f) * (leather ? 0.07f : 0.15f);
                var color = leather ? new Color(0.20f + grain - wear, 0.115f + grain - wear * 0.5f, 0.065f + grain)
                    : new Color(0.94f - grain - wear, 0.875f - grain - wear, 0.715f - grain - wear * 0.8f);
                if (edge == 6 || edge == 9) color = leather ? new Color(0.57f, 0.40f, 0.16f) : new Color(0.64f, 0.45f, 0.25f);
                if (!leather && x < 18) color *= 0.82f + x / 100f;
                color.a = 1;
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }

        private void ClearArt()
        {
            foreach (var texture in _art.Values) if (texture != null) Destroy(texture);
            _art.Clear();
        }

        private static string Roman(int value)
        {
            if (value <= 0 || value > 3999) return value.ToString();
            var result = new System.Text.StringBuilder();
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            for (int i = 0; i < values.Length; i++) while (value >= values[i]) { result.Append(symbols[i]); value -= values[i]; }
            return result.ToString();
        }

        private void OnDestroy()
        {
            ClearArt();
            if (_bookFont != null) Destroy(_bookFont);
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_paperTexture != null) Destroy(_paperTexture);
            if (_coverTexture != null) Destroy(_coverTexture);
            ChronicleRuntime.OnWorldChanging();
        }
    }
}
