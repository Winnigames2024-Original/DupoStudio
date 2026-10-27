using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DupoStudio
{
    /// <summary>Дерево проекта — встраивается слева в окно проекта.</summary>
    public class ExplorerForm : Form
    {
        public readonly TreeView Tree = new TreeView();

        /// <summary>Пользователь открыл файл двойным щелчком.</summary>
        public event EventHandler<string> FileActivated;

        static readonly string[] Skip =
            { "build", "bin", "obj", ".git", ".vs", "runtime", "node_modules" };

        public ExplorerForm()
        {
            Text = "Проект";
            FormBorderStyle = FormBorderStyle.None;
            TopLevel = false;
            Dock = DockStyle.Fill;
            BackColor = Theme.Panel;

            Tree.Dock = DockStyle.Fill;
            Tree.BackColor = Theme.Panel;
            Tree.ForeColor = Theme.Text;
            Tree.BorderStyle = BorderStyle.None;
            Tree.HideSelection = false;
            Tree.Font = new Font("Segoe UI", 9.5f);
            Tree.ItemHeight = 24;
            Tree.Indent = 18;
            Tree.ShowLines = false;
            Tree.FullRowSelect = true;

            Tree.NodeMouseDoubleClick += delegate (object s, TreeNodeMouseClickEventArgs e)
            {
                string path = e.Node.Tag as string;
                if (path == null || !File.Exists(path)) return;

                var h = FileActivated;
                if (h != null) h(this, path);
            };

            Controls.Add(Tree);
        }

        public void LoadProject(string projectDir)
        {
            Tree.Nodes.Clear();
            if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir)) return;

            var root = new TreeNode(Path.GetFileName(projectDir));
            root.Tag = projectDir;
            Fill(root, projectDir);
            Tree.Nodes.Add(root);
            root.Expand();
        }

        static void Fill(TreeNode node, string dir)
        {
            foreach (string sub in Directory.GetDirectories(dir).OrderBy(x => x))
            {
                string name = Path.GetFileName(sub);
                if (Array.IndexOf(Skip, name.ToLowerInvariant()) >= 0) continue;

                var child = new TreeNode(name);
                child.Tag = sub;
                Fill(child, sub);
                node.Nodes.Add(child);
                child.Expand();
            }

            foreach (string file in Directory.GetFiles(dir).OrderBy(x => x))
            {
                var child = new TreeNode(Path.GetFileName(file));
                child.Tag = file;
                child.ForeColor = ColorFor(Path.GetExtension(file));
                node.Nodes.Add(child);
            }
        }

        public static Color ColorFor(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".dupolang": return Theme.Accent2;
                case ".dapp": return Theme.Warn;
                case ".java": return Color.FromArgb(235, 160, 90);
                case ".jar": return Color.FromArgb(200, 170, 110);
                case ".class": return Theme.Dim;
                case ".html": case ".htm": return Color.FromArgb(226, 142, 90);
                case ".php": return Color.FromArgb(165, 150, 230);
                case ".css": return Color.FromArgb(120, 170, 220);
                case ".js": return Color.FromArgb(222, 205, 120);
                case ".exe": return Theme.Ok;
                case ".md": return Theme.Dim;
                default: return Theme.Text;
            }
        }
    }
}
