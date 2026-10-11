using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BepInExModLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            L.SetLanguage("en");
            if (args.Length >= 2 && args[0] == "--scan")
            {
                var install = new Installation(args[1]); install.Scan();
                var lines = install.Files.SelectMany(f => f.Plugins.Select(p =>
                    $"{(f.Enabled ? "ON" : "OFF")}\t{p.Guid}\t{p.Name}\t{p.Version}\t{install.Key(f)}\tconfigs={p.Configs.Count}\tdependencies={string.Join(",", p.Dependencies.Select(d => d.Guid + (d.Required ? " (required)" : " (optional)")))}"));
                File.WriteAllLines(args.Length >= 3 ? args[2] : "scan-report.tsv", lines.Concat(install.Warnings.Select(w => "WARNING\t" + w)), Encoding.UTF8);
                return 0;
            }
            if (args.Length >= 3 && args[0] == "--self-test") { SelfTest.Run(args[1], args[2]); return 0; }
            if (args.Length >= 3 && args[0] == "--ui-test")
            {
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new LauncherForm(Path.Combine(args[1], "Potion Craft.exe"), Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2])), "ui-profile.json"), false))
                {
                    form.Opacity = 0; form.ShowInTaskbar = false;
                    form.ChangeLanguage(args.Length > 3 && args[3] != "test-long" ? args[3] : "en");
                    if (args.Length > 3 && args[3] == "test-long") { L.ExpandForTesting = true; form.ApplyLanguage(); }
                    if (args.Length > 4) form.Width = int.Parse(args[4]);
                    form.Scan();
                    if (!L.ExpandForTesting) form.VerifyLanguageSwitch();
                    form.Show(); Application.DoEvents(); form.PerformLayout();
                    if (args.Length > 5 && args[5] == "settings") form.ShowSettings();
                    Application.DoEvents();
                    form.VerifyButtonLayout();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(args[2]); }
                }
                return 0;
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm(args.Length > 0 ? args[0] : null)); return 0;
        }
        catch (Exception e)
        {
            if (args.Length > 0 && args[0].StartsWith("--")) File.WriteAllText("launcher-error.txt", e.ToString());
            else MessageBox.Show(e.Message, L.T("Launcher error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

public sealed class LauncherForm : Form
{
    private readonly TextBox exe = new TextBox { Dock = DockStyle.Fill };
    private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AllowUserToResizeRows = false, AllowUserToResizeColumns = false, AllowUserToOrderColumns = false,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable, EditMode = DataGridViewEditMode.EditOnEnter };
    private readonly TextBox details = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = new Label { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
    private readonly ComboBox configs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400 };
    private readonly string profilePath;
    private Profile profile;
    private Installation install;
    private bool updatingSelection;
    private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill };
    private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private bool updatingLanguage;
    public LauncherForm(string initialExe, string profileOverride = null, bool scanOnShow = true)
    {
        profilePath = profileOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BepInExModLauncher", "profile.json");
        Text = L.T("BepInEx Mod Launcher · Demo"); Width = 1180; Height = 780; MinimumSize = new Size(900, 600);
        Font = new Font("Segoe UI", 9F); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterScreen;
        profile = Profile.Read(profilePath);
        L.SetLanguage(profile.Language);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(exe, 0, 0); top.Controls.Add(Button("Select game", Browse), 1, 0); top.Controls.Add(Button("Rescan", Scan), 2, 0);
        var bottom = Flow(); bottom.Padding = new Padding(8);
        bottom.Controls.Add(Button("Enable all", () => SetAll(true))); bottom.Controls.Add(Button("Disable all", () => SetAll(false)));
        bottom.Controls.Add(Button("Apply selection", () => Apply(false))); bottom.Controls.Add(Button("Apply and launch game", () => Apply(true)));
        bottom.Controls.Add(Button("Recover interrupted operation", Recover));
        var configBar = Flow();
        configBar.Controls.Add(configs); configBar.Controls.Add(Button("Edit configuration", EditConfig)); configBar.Controls.Add(Button("Open file location", OpenLocation));
        var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); detailLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailLayout.Controls.Add(details, 0, 0); detailLayout.Controls.Add(configBar, 0, 1);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new Size(1000, 550), SplitterDistance = 290,
            Panel1MinSize = 120, Panel2MinSize = 240 };
        split.SizeChanged += (s, e) => { if (split.Height >= split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth)
            split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min((int)(split.Height * 0.52), split.Height - split.Panel2MinSize - split.SplitterWidth)); };
        split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(detailLayout);
        var pluginsPage = new TabPage { Tag = "Plugins" }; pluginsPage.Controls.Add(split);
        var settingsPage = new TabPage { Tag = "Settings", AutoScroll = true };
        var settingsLayout = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
        var languageRow = Flow(); languageRow.Dock = DockStyle.None; languageRow.Controls.Add(Caption("Language:")); languageRow.Controls.Add(language);
        settingsLayout.Controls.Add(languageRow);
        foreach (var key in new[] { "Language changes apply immediately and are saved for the next launch.", "English is the default language. Following the system language is optional.", "Language settings affect the launcher. DLL metadata and configuration contents keep their original text." })
            settingsLayout.Controls.Add(Caption(key));
        settingsLayout.SizeChanged += (s, e) => { foreach (Control control in settingsLayout.Controls) if (control is Label label) label.MaximumSize = new Size(Math.Max(100, settingsLayout.ClientSize.Width - 40), 0); };
        settingsPage.Controls.Add(settingsLayout); tabs.TabPages.Add(pluginsPage); tabs.TabPages.Add(settingsPage);
        root.Controls.Add(top, 0, 0); root.Controls.Add(tabs, 0, 1); root.Controls.Add(bottom, 0, 2); root.Controls.Add(status, 0, 3); Controls.Add(root);
        root.SizeChanged += (s, e) => status.MaximumSize = new Size(Math.Max(100, root.ClientSize.Width - 8), 0);
        language.SelectedIndexChanged += (s, e) => { if (!updatingLanguage && language.SelectedItem is LanguageChoice choice) Guard(() => ChangeLanguage(choice.Code)); };
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "selected", Tag = "Enable next launch", FillWeight = 14, MinimumWidth = 90 });
        foreach (var pair in new[] { ("name", "Plugin name", 65, 200), ("version", "Version", 20, 70), ("state", "Current state", 18, 90), ("path", "Relative path", 65, 220) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = pair.Item1, Tag = pair.Item2, FillWeight = pair.Item3, MinimumWidth = pair.Item4, ReadOnly = true });
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.SelectionChanged += (s, e) => ShowDetails();
        grid.CellValueChanged += (s, e) => {
            if (updatingSelection || e.RowIndex < 0 || e.ColumnIndex != grid.Columns["selected"].Index || install == null) return;
            var row = grid.Rows[e.RowIndex]; var file = row.Tag as ModFile; if (file == null) return;
            try
            {
                var changes = DependencySelection.Set(install, file, Convert.ToBoolean(row.Cells["selected"].Value), exe.Text);
                status.Text = changes.Count > 1 ? L.T("Dependencies synchronized: ") + string.Join(", ", changes.Select(f => f.Name + (f.Selected ? L.T(" (enabled)") : L.T(" (disabled)")))) : L.T("Selection takes effect on the next launch");
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, L.T("Dependency resolution failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { RefreshSelection(); }
        };
        exe.Text = GameLocation.Resolve(initialExe, profile.GameExe);
        ApplyLanguage();
        Shown += (s, e) => { if (scanOnShow && File.Exists(exe.Text)) Guard(Scan); };
    }
    private Button Button(string text, Action action)
    {
        var button = new Button { Text = L.T(text), Tag = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 5, 8, 5) };
        button.Click += (s, e) => Guard(action); return button;
    }
    private static FlowLayoutPanel Flow() => new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(4) };
    private static Label Caption(string key) => new Label { Text = L.T(key), Tag = key, AutoSize = true, Margin = new Padding(3, 8, 3, 8) };
    private sealed class LanguageChoice
    {
        public string Code;
        public override string ToString() => Code == "auto" ? L.T("Follow system language") : Code == "en" ? "English" : "简体中文";
    }
    internal void ShowSettings() { tabs.SelectedIndex = 1; }
    internal void VerifyLanguageSwitch()
    {
        var preference = profile.Language;
        var selections = install.Files.Select(f => f.Selected).ToArray();
        var metadata = install.Files.SelectMany(f => f.Plugins).Select(p => p.Guid + "|" + p.Name + "|" + p.Version).ToArray();
        ChangeLanguage(preference == "en" ? "zh-CN" : "en");
        if (!selections.SequenceEqual(install.Files.Select(f => f.Selected)) || !metadata.SequenceEqual(install.Files.SelectMany(f => f.Plugins).Select(p => p.Guid + "|" + p.Name + "|" + p.Version)))
            throw new InvalidOperationException("Language switching changed selections or DLL metadata.");
        ChangeLanguage(preference);
        if (Profile.Read(profilePath).Language != preference) throw new InvalidOperationException("Language preference was not saved.");
    }
    internal void VerifyButtonLayout()
    {
        void Verify(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                if (child is Button && !parent.ClientRectangle.Contains(child.Bounds))
                    throw new InvalidOperationException("Button is clipped by its container: " + child.Text);
                Verify(child);
            }
        }
        Verify(this);
    }
    internal void ChangeLanguage(string preference)
    {
        var previous = profile.Language;
        profile.Language = preference;
        try { Json.Write(profilePath, profile); }
        catch { profile.Language = previous; throw; }
        L.SetLanguage(preference); ApplyLanguage();
    }
    internal void ApplyLanguage()
    {
        SuspendLayout();
        void Translate(Control parent)
        {
            if (parent.Tag is string key) parent.Text = L.T(key);
            foreach (Control child in parent.Controls) Translate(child);
        }
        Translate(this); Text = L.T("BepInEx Mod Launcher · Demo");
        foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = L.T((string)column.Tag);
        updatingLanguage = true;
        language.Items.Clear();
        foreach (var code in new[] { "auto", "en", "zh-CN" }) language.Items.Add(new LanguageChoice { Code = code });
        language.SelectedIndex = profile.Language == "auto" ? 0 : profile.Language == "zh-CN" ? 2 : 1;
        updatingLanguage = false;
        foreach (DataGridViewRow row in grid.Rows) if (row.Tag is ModFile file)
            row.Cells["state"].Value = file.Enabled ? L.T("Enabled") : L.T("Disabled");
        ShowDetails();
        UpdateStatus(); ResumeLayout(true);
    }
    private void UpdateStatus()
    {
        if (install == null) { status.Text = L.T("Select a game executable, then scan for plugins."); return; }
        var core = Path.Combine(install.Root, "BepInEx", "core", "BepInEx.dll");
        var version = File.Exists(core) ? FileVersionInfo.GetVersionInfo(core).FileVersion : L.T("Unknown");
        status.Text = L.T("BepInEx {0} · {1} plugins / {2} DLLs · {3} supporting libraries · {4} scan warnings", version,
            install.Files.Sum(f => f.Plugins.Count), install.Files.Count, install.LibraryCount, install.Warnings.Count) +
            (File.Exists(install.Journal) ? L.T(" · Interrupted operation found; recover first") : L.T(" · Selection takes effect on the next launch"));
    }
    private void Guard(Action action)
    {
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, L.T("Operation failed"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void Browse()
    {
        using (var dialog = new OpenFileDialog { Filter = L.T("Game executable (*.exe)|*.exe"), Title = L.T("Select the main game executable") })
            if (dialog.ShowDialog(this) == DialogResult.OK) { exe.Text = dialog.FileName; Scan(); }
    }
    internal void Scan()
    {
        var game = Path.GetFullPath(exe.Text.Trim());
        if (!File.Exists(game)) throw new IOException(L.T("Please select the main game executable."));
        install = new Installation(Path.GetDirectoryName(game)); install.Scan();
        grid.Rows.Clear();
        foreach (var file in install.Files)
        {
            var index = grid.Rows.Add(file.Selected, file.Name, file.Version, file.Enabled ? L.T("Enabled") : L.T("Disabled"), install.Key(file));
            grid.Rows[index].Tag = file;
        }
        profile.GameExe = game; Json.Write(profilePath, profile);
        UpdateStatus();
        ShowDetails();
    }
    private ModFile Current => grid.CurrentRow?.Tag as ModFile;
    private void ShowDetails()
    {
        configs.Items.Clear();
        var file = Current; if (file == null) { details.Clear(); return; }
        var text = new StringBuilder();
        text.AppendLine(L.T("File: ") + file.Path);
        text.AppendLine(L.T("Assembly: ") + file.AssemblyName).AppendLine(L.T("Assembly version: ") + file.AssemblyVersion);
        if (!string.IsNullOrWhiteSpace(file.Company)) text.AppendLine(L.T("Company (assembly metadata): ") + file.Company);
        if (!string.IsNullOrWhiteSpace(file.Description)) text.AppendLine(L.T("Description: ") + file.Description);
        if (file.Plugins.Count > 1) text.AppendLine(L.T("This DLL contains multiple plugins; they are enabled or disabled together."));
        foreach (var mod in file.Plugins)
        {
            text.AppendLine().AppendLine(mod.Name + " " + mod.Version).AppendLine("GUID: " + mod.Guid);
            foreach (var dep in mod.Dependencies) text.AppendLine(DependencySelection.Describe(install, dep));
            foreach (var dependent in install.Files.SelectMany(f => f.Plugins).Where(p => p.Dependencies.Any(d => d.Required && d.Guid == mod.Guid)))
                text.AppendLine(L.T("Required by: ") + dependent.Name);
            foreach (var conflict in mod.Conflicts) text.AppendLine(L.T("Declared conflict: ") + conflict);
            foreach (var process in mod.Processes) text.AppendLine(L.T("Target process: ") + process);
            if (mod.Configs.Count == 0) text.AppendLine(L.T("No standard configuration found (it may not exist yet or may use a custom format)."));
            foreach (var config in mod.Configs) if (!configs.Items.Contains(config)) configs.Items.Add(config);
        }
        if (install.Warnings.Count > 0) text.AppendLine().AppendLine(L.T("Scan warnings:")).AppendLine(string.Join(Environment.NewLine, install.Warnings));
        details.Text = text.ToString(); if (configs.Items.Count > 0) configs.SelectedIndex = 0;
    }
    private void RefreshSelection()
    {
        updatingSelection = true;
        try { foreach (DataGridViewRow row in grid.Rows) if (row.Tag is ModFile file) row.Cells["selected"].Value = file.Selected; }
        finally { updatingSelection = false; }
        ShowDetails();
    }
    private void SetAll(bool selected)
    {
        if (install == null) return;
        foreach (var file in install.Files) file.Selected = selected;
        RefreshSelection();
        var errors = install.Validate(exe.Text);
        status.Text = errors.Count == 0 ? L.T("Selection takes effect on the next launch") : L.T("Resolve dependency or conflict issues before applying this selection.");
    }
    private void Apply(bool launch)
    {
        if (install == null) throw new IOException(L.T("Please scan for plugins first."));
        var game = Path.GetFullPath(exe.Text.Trim());
        if (!string.Equals(Path.GetDirectoryName(game), install.Root, StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("The game path changed. Please scan again."));
        grid.EndEdit(); foreach (DataGridViewRow row in grid.Rows) ((ModFile)row.Tag).Selected = Convert.ToBoolean(row.Cells["selected"].Value);
        install.Apply(game); Scan();
        if (launch) Process.Start(new ProcessStartInfo(game) { WorkingDirectory = install.Root, UseShellExecute = true });
        else status.Text += L.T(" · Applied");
    }
    private void Recover() { if (install == null) return; install.Recover(exe.Text); Scan(); }
    private void OpenLocation()
    {
        if (Current == null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + (Current.Enabled ? Current.Path : Current.Path + ".disabled") + "\"") { UseShellExecute = true });
    }
    private void EditConfig()
    {
        if (!(configs.SelectedItem is string path)) return;
        using (var editor = new ConfigForm(path, exe.Text)) editor.ShowDialog(this);
    }
}

public sealed class ConfigForm : Form
{
    public ConfigForm(string path, string gameExe)
    {
        var document = new ConfigDocument(path);
        Text = L.T("Configuration editor · ") + Path.GetFileName(path); Width = 900; Height = 700; MinimumSize = new Size(640, 480);
        Font = new Font("Segoe UI", 9F); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
        var editor = new TextBox { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both,
            WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 11), Text = document.Text, MaxLength = int.MaxValue };
        var save = new Button { Text = L.T("Save configuration (automatic backup)"), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var label = new Label { Text = L.T("Edit the original text, preserving sections, comments, and unknown keys. Changes take effect on the next launch."), Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        save.Click += (s, e) => {
            try { Installation.EnsureStopped(gameExe); document.Save(editor.Text); MessageBox.Show(this, L.T("Saved. The original configuration was backed up in the same directory.")); }
            catch (Exception error) { MessageBox.Show(this, error.Message, L.T("Save failed")); }
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(label, 0, 0); layout.Controls.Add(editor, 0, 1); layout.Controls.Add(save, 0, 2); Controls.Add(layout);
        layout.SizeChanged += (s, e) => label.MaximumSize = new Size(Math.Max(100, layout.ClientSize.Width - 8), 0);
        FormClosing += (s, e) => {
            if (editor.Text != document.Text && MessageBox.Show(this, L.T("Discard unsaved changes?"), L.T("Configuration editor"), MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true;
        };
    }
}
