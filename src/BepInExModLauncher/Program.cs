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
                    form.Scan();
                    form.Show(); Application.DoEvents(); form.PerformLayout();
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
            else MessageBox.Show(e.Message, "启动器错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(8), Text = "选择游戏 EXE 后扫描模组。" };
    private readonly TextBox category = new TextBox { Width = 180 };
    private readonly TextBox notes = new TextBox { Width = 280 };
    private readonly ComboBox configs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 400 };
    private readonly string profilePath;
    private Profile profile;
    private Installation install;
    private bool updatingSelection;
    public LauncherForm(string initialExe, string profileOverride = null, bool scanOnShow = true)
    {
        profilePath = profileOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BepInExModLauncher", "profile.json");
        Text = "BepInEx 模组启动器 · Demo"; Width = 1180; Height = 780; MinimumSize = new Size(900, 600);
        Font = new Font("Microsoft YaHei UI", 9F); StartPosition = FormStartPosition.CenterScreen;
        profile = Profile.Read(profilePath);
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 3, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        top.Controls.Add(exe, 0, 0); top.Controls.Add(Button("选择游戏", Browse), 1, 0); top.Controls.Add(Button("重新扫描", Scan), 2, 0);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
        bottom.Controls.Add(Button("全部启用", () => SetAll(true))); bottom.Controls.Add(Button("全部禁用", () => SetAll(false)));
        bottom.Controls.Add(Button("应用选择", () => Apply(false))); bottom.Controls.Add(Button("应用并启动游戏", () => Apply(true)));
        bottom.Controls.Add(Button("恢复未完成操作", Recover));
        var annotation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        annotation.Controls.Add(new Label { Text = "本地标签：", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); annotation.Controls.Add(category);
        annotation.Controls.Add(notes); annotation.Controls.Add(Button("保存标签／备注", SaveAnnotation));
        var configBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(4) };
        configBar.Controls.Add(configs); configBar.Controls.Add(Button("编辑配置", EditConfig)); configBar.Controls.Add(Button("打开文件位置", OpenLocation));
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 340 };
        split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(details); split.Panel2.Controls.Add(annotation); split.Panel2.Controls.Add(configBar);
        Controls.Add(split); Controls.Add(top); Controls.Add(bottom); Controls.Add(status);
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "selected", HeaderText = "下次启用", FillWeight = 14 });
        foreach (var pair in new[] { ("name", "模组名称", 65), ("version", "版本", 20), ("state", "当前状态", 18), ("category", "本地标签／文件变化", 45), ("path", "相对路径", 65) })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = pair.Item1, HeaderText = pair.Item2, FillWeight = pair.Item3, ReadOnly = true });
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.SelectionChanged += (s, e) => ShowDetails();
        grid.CellValueChanged += (s, e) => {
            if (updatingSelection || e.RowIndex < 0 || e.ColumnIndex != grid.Columns["selected"].Index || install == null) return;
            var row = grid.Rows[e.RowIndex]; var file = row.Tag as ModFile; if (file == null) return;
            try
            {
                var changes = DependencySelection.Set(install, file, Convert.ToBoolean(row.Cells["selected"].Value), exe.Text);
                status.Text = changes.Count > 1 ? "依赖关系已同步：" + string.Join("、", changes.Select(f => f.Name + (f.Selected ? "（启用）" : "（禁用）"))) : "选择将在下次启动生效";
            }
            catch (Exception error) { MessageBox.Show(this, error.Message, "依赖解析未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally { RefreshSelection(); }
        };
        exe.Text = GameLocation.Resolve(initialExe, profile.GameExe);
        Shown += (s, e) => { if (scanOnShow && File.Exists(exe.Text)) Guard(Scan); };
    }
    private Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 28 };
        button.Click += (s, e) => Guard(action); return button;
    }
    private void Guard(Action action)
    {
        try { action(); } catch (Exception e) { MessageBox.Show(this, e.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private void Browse()
    {
        using (var dialog = new OpenFileDialog { Filter = "游戏程序 (*.exe)|*.exe", Title = "选择游戏主程序" })
            if (dialog.ShowDialog(this) == DialogResult.OK) { exe.Text = dialog.FileName; Scan(); }
    }
    internal void Scan()
    {
        var game = Path.GetFullPath(exe.Text.Trim());
        if (!File.Exists(game)) throw new IOException("请选择游戏主程序。");
        install = new Installation(Path.GetDirectoryName(game)); install.Scan();
        grid.Rows.Clear();
        foreach (var file in install.Files)
        {
            if (!profile.Annotations.ContainsKey(AnnotationKey(file)))
                profile.Annotations[AnnotationKey(file)] = new Annotation { Hash = file.Hash };
            var index = grid.Rows.Add(file.Selected, file.Name, file.Version, file.Enabled ? "启用" : "禁用", SourceLabel(file), install.Key(file));
            grid.Rows[index].Tag = file;
        }
        profile.GameExe = game; Json.Write(profilePath, profile);
        var core = Path.Combine(install.Root, "BepInEx", "core", "BepInEx.dll");
        var version = File.Exists(core) ? FileVersionInfo.GetVersionInfo(core).FileVersion : "未知";
        status.Text = $"BepInEx {version} · {install.Files.Sum(f => f.Plugins.Count)} 个插件 / {install.Files.Count} 个 DLL · {install.LibraryCount} 个辅助库 · {install.Warnings.Count} 条扫描提示" +
            (File.Exists(install.Journal) ? " · 存在未完成操作，请恢复" : " · 选择将在下次启动生效");
        ShowDetails();
    }
    private string AnnotationKey(ModFile file) => install.Root + "|" + install.Key(file);
    private Annotation GetAnnotation(ModFile file) => profile.Annotations.TryGetValue(AnnotationKey(file), out var value) ? value : new Annotation();
    private string SourceLabel(ModFile file)
    {
        var annotation = GetAnnotation(file);
        return (string.IsNullOrWhiteSpace(annotation.Category) ? "未标注" : annotation.Category) + (annotation.Hash != "" && annotation.Hash != file.Hash ? " · 文件已变化" : "");
    }
    private ModFile Current => grid.CurrentRow?.Tag as ModFile;
    private void ShowDetails()
    {
        configs.Items.Clear();
        var file = Current; if (file == null) { details.Clear(); return; }
        var annotation = GetAnnotation(file); category.Text = annotation.Category; notes.Text = annotation.Notes;
        var text = new StringBuilder();
        text.AppendLine("文件：" + file.Path).AppendLine("SHA-256：" + file.Hash);
        if (file.Plugins.Count > 1) text.AppendLine("此 DLL 包含多个插件，将一起启停。");
        foreach (var mod in file.Plugins)
        {
            text.AppendLine().AppendLine(mod.Name + " " + mod.Version).AppendLine("GUID：" + mod.Guid);
            foreach (var dep in mod.Dependencies) text.AppendLine(DependencySelection.Describe(install, dep));
            foreach (var dependent in install.Files.SelectMany(f => f.Plugins).Where(p => p.Dependencies.Any(d => d.Required && d.Guid == mod.Guid)))
                text.AppendLine("被依赖：" + dependent.Name);
            foreach (var conflict in mod.Conflicts) text.AppendLine("声明冲突：" + conflict);
            foreach (var process in mod.Processes) text.AppendLine("适用进程：" + process);
            if (mod.Configs.Count == 0) text.AppendLine("未发现关联的标准配置（可能尚未生成或使用自定义格式）。");
            foreach (var config in mod.Configs) if (!configs.Items.Contains(config)) configs.Items.Add(config);
        }
        if (install.Warnings.Count > 0) text.AppendLine().AppendLine("扫描提示：").AppendLine(string.Join(Environment.NewLine, install.Warnings));
        details.Text = text.ToString(); if (configs.Items.Count > 0) configs.SelectedIndex = 0;
    }
    private void SaveAnnotation()
    {
        var file = Current; if (file == null) return;
        profile.Annotations[AnnotationKey(file)] = new Annotation { Category = category.Text.Trim(), Notes = notes.Text, Hash = file.Hash };
        Json.Write(profilePath, profile); grid.CurrentRow.Cells["category"].Value = SourceLabel(file);
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
        status.Text = errors.Count == 0 ? "选择将在下次启动生效" : "当前选择有依赖或冲突问题，应用前需要处理。";
    }
    private void Apply(bool launch)
    {
        if (install == null) throw new IOException("请先扫描模组。");
        var game = Path.GetFullPath(exe.Text.Trim());
        if (!string.Equals(Path.GetDirectoryName(game), install.Root, StringComparison.OrdinalIgnoreCase)) throw new IOException("游戏路径已改变，请重新扫描。");
        grid.EndEdit(); foreach (DataGridViewRow row in grid.Rows) ((ModFile)row.Tag).Selected = Convert.ToBoolean(row.Cells["selected"].Value);
        install.Apply(game); Scan();
        if (launch) Process.Start(new ProcessStartInfo(game) { WorkingDirectory = install.Root, UseShellExecute = true });
        else status.Text += " · 已应用";
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
        Text = "配置编辑 · " + Path.GetFileName(path); Width = 900; Height = 700; StartPosition = FormStartPosition.CenterParent;
        var editor = new TextBox { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both,
            WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 11), Text = document.Text, MaxLength = int.MaxValue };
        var save = new Button { Text = "保存配置（自动备份）", Dock = DockStyle.Bottom, Height = 40 };
        var label = new Label { Text = "按原文编辑：保留分组、注释和未知配置项。保存后下次启动生效。", Dock = DockStyle.Top, Height = 32 };
        save.Click += (s, e) => {
            try { Installation.EnsureStopped(gameExe); document.Save(editor.Text); MessageBox.Show(this, "已保存，原配置备份在同一目录。"); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "保存未完成"); }
        };
        Controls.Add(editor); Controls.Add(save); Controls.Add(label);
        FormClosing += (s, e) => {
            if (editor.Text != document.Text && MessageBox.Show(this, "有未保存的更改，是否放弃？", "配置编辑", MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true;
        };
    }
}
