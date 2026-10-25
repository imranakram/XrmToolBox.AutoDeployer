namespace XrmToolBox.AutoDeployer
{
    partial class MainControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainControl));
            this.tsMenu = new System.Windows.Forms.ToolStrip();
            this.bProject = new System.Windows.Forms.ToolStripDropDownButton();
            this.bOpen = new System.Windows.Forms.ToolStripButton();
            this.bSave = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.bAdd = new System.Windows.Forms.ToolStripDropDownButton();
            this.bAddPluginMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bAddWebResourceMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bAddPackageMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.bDelSelected = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsWrSummary = new System.Windows.Forms.ToolStripLabel();
            this.ofdPlugin = new System.Windows.Forms.OpenFileDialog();
            this.listWatching = new System.Windows.Forms.ListView();
            this.file = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.folder = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.fileupdated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.pluginupdated = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.status = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.tsMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tsMenu
            // 
            this.tsMenu.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.tsMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bProject,
            this.toolStripSeparator1,
            this.bAdd,
            this.bDelSelected,
            this.toolStripSeparator2,
            this.tsWrSummary});
            this.tsMenu.Location = new System.Drawing.Point(0, 0);
            this.tsMenu.Name = "tsMenu";
            this.tsMenu.Size = new System.Drawing.Size(1061, 31);
            this.tsMenu.TabIndex = 0;
            this.tsMenu.Text = "toolStrip1";
            // 
            // bProject
            // 
            this.bProject.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bOpen,
            this.bSave});
            this.bProject.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.package_24;
            this.bProject.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bProject.Name = "bProject";
            this.bProject.Size = new System.Drawing.Size(81, 28);
            this.bProject.Text = "Project";
            this.bProject.ToolTipText = "Project is where we can store assemblies and packages to be opened easily.";
            // 
            // bOpen
            // 
            this.bOpen.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.folder_24;
            this.bOpen.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bOpen.Name = "bOpen";
            this.bOpen.Size = new System.Drawing.Size(73, 28);
            this.bOpen.Text = "Open...";
            this.bOpen.ToolTipText = "Open stored Auto Deployer files to watch.";
            this.bOpen.Click += new System.EventHandler(this.bOpen_Click);
            // 
            // bSave
            // 
            this.bSave.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.disk;
            this.bSave.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bSave.Name = "bSave";
            this.bSave.Size = new System.Drawing.Size(68, 28);
            this.bSave.Text = "Save...";
            this.bSave.ToolTipText = "Save list of watched files to can get opened for next time.";
            this.bSave.Click += new System.EventHandler(this.bSave_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 31);
            // 
            // bAdd
            // 
            this.bAdd.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.bAddPluginMenuItem,
            this.bAddWebResourceMenuItem,
            this.bAddPackageMenuItem});
            this.bAdd.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.navigate_plus;
            this.bAdd.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bAdd.Name = "bAdd";
            this.bAdd.Size = new System.Drawing.Size(66, 28);
            this.bAdd.Text = "Add";
            this.bAdd.ToolTipText = "Add plugin assembly or web resource. Web Resource require extra info for their CR" +
    "M prefix, root path and so on...";
            // 
            // bAddPluginMenuItem
            // 
            this.bAddPluginMenuItem.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.assembly;
            this.bAddPluginMenuItem.Name = "bAddPluginMenuItem";
            this.bAddPluginMenuItem.Size = new System.Drawing.Size(162, 22);
            this.bAddPluginMenuItem.Text = "Plugin Assembly";
            this.bAddPluginMenuItem.Click += new System.EventHandler(this.bAddPluginMenuItem_Click);
            // 
            // bAddWebResourceMenuItem
            // 
            this.bAddWebResourceMenuItem.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.webresource;
            this.bAddWebResourceMenuItem.Name = "bAddWebResourceMenuItem";
            this.bAddWebResourceMenuItem.Size = new System.Drawing.Size(162, 22);
            this.bAddWebResourceMenuItem.Text = "Web Resources";
            this.bAddWebResourceMenuItem.Click += new System.EventHandler(this.bAddWebResourceMenuItem_Click);
            // 
            // bAddPackageMenuItem
            // 
            this.bAddPackageMenuItem.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.pluginpackage;
            this.bAddPackageMenuItem.Name = "bAddPackageMenuItem";
            this.bAddPackageMenuItem.Size = new System.Drawing.Size(162, 22);
            this.bAddPackageMenuItem.Text = "Plugin Package";
            this.bAddPackageMenuItem.Click += new System.EventHandler(this.bAddPackageMenuItem_Click);
            // 
            // bDelSelected
            // 
            this.bDelSelected.Enabled = false;
            this.bDelSelected.Image = global::XrmToolBox.AutoDeployer.Properties.Resources.delete2;
            this.bDelSelected.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.bDelSelected.Name = "bDelSelected";
            this.bDelSelected.Size = new System.Drawing.Size(124, 28);
            this.bDelSelected.Text = "Remove selected";
            this.bDelSelected.ToolTipText = "Remove selected plugin or web resource";
            this.bDelSelected.Click += new System.EventHandler(this.bDelSelected_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 31);
            // 
            // tsWrSummary
            // 
            this.tsWrSummary.Name = "tsWrSummary";
            this.tsWrSummary.Size = new System.Drawing.Size(0, 28);
            // 
            // ofdPlugin
            // 
            this.ofdPlugin.Filter = "Dataverse Plugin file|*.dll";
            this.ofdPlugin.Title = "Select Dataverse Plugin file";
            // 
            // listWatching
            // 
            this.listWatching.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.file,
            this.folder,
            this.fileupdated,
            this.pluginupdated,
            this.status});
            this.listWatching.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listWatching.FullRowSelect = true;
            this.listWatching.HideSelection = false;
            this.listWatching.Location = new System.Drawing.Point(0, 0);
            this.listWatching.Name = "listWatching";
            this.listWatching.Size = new System.Drawing.Size(1061, 187);
            this.listWatching.TabIndex = 3;
            this.listWatching.UseCompatibleStateImageBehavior = false;
            this.listWatching.View = System.Windows.Forms.View.Details;
            this.listWatching.SelectedIndexChanged += new System.EventHandler(this.listWatching_SelectedIndexChanged);
            // 
            // file
            // 
            this.file.Text = "Artifact";
            this.file.Width = 250;
            // 
            // folder
            // 
            this.folder.Text = "Folder";
            this.folder.Width = 415;
            // 
            // fileupdated
            // 
            this.fileupdated.Text = "File Updated";
            this.fileupdated.Width = 110;
            // 
            // pluginupdated
            // 
            this.pluginupdated.Text = "Updated";
            this.pluginupdated.Width = 115;
            // 
            // status
            // 
            this.status.Text = "Status";
            this.status.Width = 156;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 31);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.listWatching);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.txtLog);
            this.splitContainer1.Size = new System.Drawing.Size(1061, 325);
            this.splitContainer1.SplitterDistance = 187;
            this.splitContainer1.SplitterWidth = 8;
            this.splitContainer1.TabIndex = 4;
            // 
            // txtLog
            // 
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Location = new System.Drawing.Point(0, 0);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new System.Drawing.Size(1061, 130);
            this.txtLog.TabIndex = 0;
            // 
            // MainControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.tsMenu);
            this.Name = "MainControl";
            this.PluginIcon = ((System.Drawing.Icon)(resources.GetObject("$this.PluginIcon")));
            this.Size = new System.Drawing.Size(1061, 356);
            this.tsMenu.ResumeLayout(false);
            this.tsMenu.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip tsMenu;
        private System.Windows.Forms.OpenFileDialog ofdPlugin;
        private System.Windows.Forms.ListView listWatching;
        private System.Windows.Forms.ToolStripDropDownButton bAdd;
        private System.Windows.Forms.ToolStripMenuItem bAddPluginMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bAddWebResourceMenuItem;
        private System.Windows.Forms.ToolStripButton bDelSelected;
        private System.Windows.Forms.ColumnHeader file;
        private System.Windows.Forms.ColumnHeader folder;
        private System.Windows.Forms.ColumnHeader fileupdated;
        private System.Windows.Forms.ColumnHeader pluginupdated;
        private System.Windows.Forms.ColumnHeader status;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.ToolStripLabel tsWrSummary;
        private System.Windows.Forms.ToolStripMenuItem bAddPackageMenuItem;
        private System.Windows.Forms.ToolStripButton bOpen;
        private System.Windows.Forms.ToolStripButton bSave;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripDropDownButton bProject;
    }
}
