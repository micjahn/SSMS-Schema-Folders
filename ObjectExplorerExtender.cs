namespace SsmsSchemaFolders
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Windows.Forms;

    using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

    /// <summary>
    /// Used to organize Databases and Tables in Object Explorer into groups
    /// </summary>
    public class ObjectExplorerExtender : IObjectExplorerExtender
    {
        private ISchemaFolderOptions Options { get; }
        private IServiceProvider Package { get; }

        /// <summary>
        /// 
        /// </summary>
        public ObjectExplorerExtender(IServiceProvider package, ISchemaFolderOptions options)
        {
            Package = package;
            Options = options;
        }


        /// <summary>
        /// Gets the underlying object which is responsible for displaying object explorer structure
        /// </summary>
        /// <returns></returns>
        public TreeView GetObjectExplorerTreeView()
        {
            var objectExplorerService = (IObjectExplorerService) Package.GetService(typeof(IObjectExplorerService));
            if (objectExplorerService != null)
            {
                var oesTreeProperty = objectExplorerService.GetType().GetProperty("Tree", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (oesTreeProperty != null)
                    return (TreeView) oesTreeProperty.GetValue(objectExplorerService, null);
            }

            return null;
        }

        public void RefreshTableNode()
        {
            var objectExplorer = (IObjectExplorerService)Package.GetService(typeof(IObjectExplorerService));
            var treeView = GetObjectExplorerTreeView();

            if (treeView != null)
            {
                foreach (TreeNode node in treeView.Nodes)
                {
                    if (RefreshTableNodebyNode(objectExplorer, node))
                        break;
                }
            }
        }

        private bool RefreshTableNodebyNode(IObjectExplorerService objectExplorer, TreeNode node)
        {
            if (node.IsExpanded && NodeIsDatabaseNode(node))
            {
                var nodeInformation = GetNodeInformation(node);
                objectExplorer.SynchronizeTree(nodeInformation);
                SendKeys.Send("{F5}");
                return true;
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                if (RefreshTableNodebyNode(objectExplorer, childNode))
                    return true;
            }
            return false;
        }
        private bool NodeIsDatabaseNode(TreeNode node)
        {
            if (node?.Parent != null)
            {
                var urnPath = GetNodeUrnPath(node);
                return urnPath == "Server/Database";
            }
            return false;
        }

        /// <summary>
        /// Gets node information from underlying type of tree node view
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <remarks>Copy of private method in ObjectExplorerService</remarks>
        private INodeInformation GetNodeInformation(TreeNode node)
        {
            INodeInformation result = null;
            IServiceProvider serviceProvider = node as IServiceProvider;
            if (serviceProvider != null)
            {
                result = (serviceProvider.GetService(typeof(INodeInformation)) as INodeInformation);
            }
            return result;
        }

        public bool GetNodeExpanding(TreeNode node)
        {
            var lazyNode = node as ILazyLoadingNode;
            if (lazyNode != null)
                return lazyNode.Expanding;
            return false;
        }

        public string GetNodeUrnPath(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            return ni?.UrnPath;
        }

        private String GetNodeSchemaQuick(TreeNode node)
        {
            var dotIndex = node.Text.IndexOf('.');
            if (dotIndex != -1)
                return node.Text.Substring(0, dotIndex);
            else
                return null;
        }

        private String GetNodeSchema(TreeNode node)
        {
            var ni = GetNodeInformation(node);
            if (ni != null)
            {
                if (Options.RegularExpressions.Count > 0)
                {
                    foreach (var pattern in Options.RegularExpressions)
                    {
                        if (String.IsNullOrEmpty(pattern))
                            continue;

                        var match = Regex.Match(ni.InvariantName, pattern, RegexOptions.IgnoreCase);
                        if (match.Groups.Count > 1)
                        {
                            for (var groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
                            {
                                var group = match.Groups[groupIndex];
                                if (!string.IsNullOrEmpty(group.Value))
                                {
                                    return ModifyCase(group.Value);
                                }
                            }
                        }
                    }
                }
                // Fallback if nothing found by reg expressions
                if (ni.InvariantName.EndsWith("." + ni.Name))
                {
                    return ModifyCase(ni.InvariantName.Substring(0, ni.InvariantName.Length - ni.Name.Length - 1));
                }
            }
            return null;
        }

        private string ModifyCase(string val)
        {
            if (Options.FolderNameCase == FolderNameCase.Lower)
                val = val.ToLower();
            if (Options.FolderNameCase == FolderNameCase.Upper)
                val = val.ToUpper();
            return val;
        }

        /// <summary>
        /// Removes schema name from object node.
        /// </summary>
        /// <param name="node">Object node to rename</param>
        public void RenameNode(TreeNode node)
        {
            // Simple method, doesn't work correctly when schema name contains a dot.
            node.Text = node.Text.Substring(node.Text.IndexOf('.') + 1);
        }

        /// <summary>
        /// Create schema nodes and move tables, functions and stored procedures under its schema node
        /// </summary>
        /// <param name="node">Table node to reorganize</param>
        /// <param name="nodeTag">Tag of new node</param>
        /// <returns>The count of schema nodes.</returns>
        public int ReorganizeNodes(TreeNode node, string nodeTag)
        {
            if (node.Nodes.Count <= 1)
                return 0;
            
            if (Options.UseClear > 0 && node.Nodes.Count >= Options.UseClear)
                return ReorganizeNodesWithClear(node, nodeTag);

            var nodeText = node.Text;
            node.Text += " (sorting...)";
            //node.TreeView.Update();

            var quickAndDirty = (Options.QuickSchema > 0 && node.Nodes.Count > Options.QuickSchema);

            //var sw = Stopwatch.StartNew();
            //debug_message("BeginUpdate:{0}", sw.ElapsedMilliseconds);

            node.TreeView.BeginUpdate();

            var unresponsive = Stopwatch.StartNew();

            //can't move nodes while iterating forward over them
            //create list of nodes to move then perform the update

            var schemas = new Dictionary<String, List<TreeNode>>();
            int schemaNodeIndex = -1;
            var newSchemaNodes = new List<TreeNode>();
            var initialNodeCount = node.Nodes.Count;

            //debug_message("Sort Nodes:{0}", sw.ElapsedMilliseconds);

            foreach (TreeNode childNode in node.Nodes)
            {
                //skip schema node folders but make sure they are in schemas list
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    if (!schemas.ContainsKey(childNode.Name))
                        schemas.Add(childNode.Name, new List<TreeNode>());

                    schemaNodeIndex = childNode.Index;

                    continue;
                }

                var schema = (quickAndDirty) ? GetNodeSchemaQuick(childNode) : GetNodeSchema(childNode);

                if (string.IsNullOrEmpty(schema))
                    continue;

                //create schema node
                if (!node.Nodes.ContainsKey(schema))
                {
                    int insertIndex = initialNodeCount;
                    // TODO: check if sorting here works with ...
                    for (; insertIndex < node.Nodes.Count; insertIndex++)
                    {
                        if (String.CompareOrdinal(node.Nodes[insertIndex].Name, schema) > 0)
                            break;
                    }

                    TreeNode schemaNode;
                    if (Options.CloneParentNode)
                    {
                        schemaNode = new SchemaFolderTreeNode(node);
                        node.Nodes.Insert(insertIndex, schemaNode);
                    }
                    else
                    {
                        schemaNode = node.Nodes.Insert(insertIndex, schema);
                    }
                    newSchemaNodes.Add(schemaNode);

                    schemaNode.Name = schema;
                    schemaNode.Text = schema;
                    schemaNode.Tag = nodeTag;

                    if (Options.AppendDot)
                        schemaNode.Text += ".";

                    if (Options.UseObjectIcon)
                    {
                        schemaNode.ImageIndex = childNode.ImageIndex;
                        schemaNode.SelectedImageIndex = childNode.ImageIndex;
                    }
                    else
                    {
                        schemaNode.ImageIndex = node.ImageIndex;
                        schemaNode.SelectedImageIndex = node.ImageIndex;
                    }
                }

                //add node to schema list
                List<TreeNode> schemaNodeList;
                if (!schemas.TryGetValue(schema, out schemaNodeList))
                {
                    schemaNodeList = new List<TreeNode>();
                    schemas.Add(schema, schemaNodeList);
                }
                schemaNodeList.Add(childNode);

                if (unresponsive.ElapsedMilliseconds > Options.UnresponsiveTimeout)
                {
                    node.TreeView.EndUpdate();
                    Application.DoEvents();
                    if (node.TreeView == null)
                        return 0;
                    node.TreeView.BeginUpdate();
                    unresponsive.Restart();
                }
            }

            //debug_message("Move Nodes:{0}", sw.ElapsedMilliseconds);

            if (schemaNodeIndex >= 0)
            {
                // TODO: ... with this merged code block
                // Move schema nodes to top of tree
                foreach (var schemaNode in newSchemaNodes)
                {
                    node.Nodes.Remove(schemaNode);
                    node.Nodes.Insert(++schemaNodeIndex, schemaNode);
                }
            }

            //move nodes to schema node
            foreach (string schema in schemas.Keys)
            {
                var schemaNode = node.Nodes[schema];
                var orderedSchemaNodes = schemas[schema].OrderBy(_ => _.Name);
                foreach (TreeNode childNode in orderedSchemaNodes)
                {
                    node.Nodes.Remove(childNode);

                    if (Options.RenameNode)
                    {
                        // Note: Node is renamed back to orginal after expanding.
                        RenameNode(childNode);
                    }
                    schemaNode.Nodes.Add(childNode);

                    if (unresponsive.ElapsedMilliseconds > Options.UnresponsiveTimeout)
                    {
                        node.TreeView.EndUpdate(); 
                        Application.DoEvents();
                        if (node.TreeView == null)
                            return 0;
                        node.TreeView.BeginUpdate();
                        unresponsive.Restart();
                }
            }
            }

            //debug_message("EndUpdate:{0}", sw.ElapsedMilliseconds);

            node.TreeView.EndUpdate();
            node.Text = nodeText;
            unresponsive.Stop();

            //debug_message("Done:{0}", sw.ElapsedMilliseconds);
            //sw.Stop();

            return schemas.Count;
        }

        /// <summary>
        /// Create schema nodes and move tables, functions and stored procedures under its schema node
        /// </summary>
        /// <param name="node">Table node to reorganize</param>
        /// <param name="nodeTag">Tag of new node</param>
        /// <returns>The count of schema nodes.</returns>
        public int ReorganizeNodesWithClear(TreeNode node, string nodeTag)
        {
            debug_message("ReorganizeNodesWithClear");

            var nodeText = node.Text;
            node.Text += " (sorting...)";
            node.TreeView.Update();

            var quickAndDirty = (Options.QuickSchema > 0 && node.Nodes.Count > Options.QuickSchema);

            var sw = Stopwatch.StartNew();
            //debug_message("Sort Nodes:{0}", sw.ElapsedMilliseconds);

            var schemas = new Dictionary<string, List<TreeNode>>();
            var schemaNodes = new Dictionary<string, TreeNode>();
            var nodeNodes = new List<TreeNode>();

            foreach (TreeNode childNode in node.Nodes)
            {
                // schema node folder
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    schemas.Add(childNode.Name, new List<TreeNode>());
                    schemaNodes.Add(childNode.Name, childNode);
                    nodeNodes.Add(childNode);
                    continue;
                }

                var schema = (quickAndDirty) ? GetNodeSchemaQuick(childNode) : GetNodeSchema(childNode);

                // other folder
                if (string.IsNullOrEmpty(schema))
                {
                    nodeNodes.Add(childNode);
                    continue;
                }

                List<TreeNode> schemaNodeList;
                if (schemas.TryGetValue(schema, out schemaNodeList))
                {
                    // add to existing schema
                    schemaNodeList.Add(childNode);
                }
                else
                {
                    // add to new schema
                    schemaNodeList = new List<TreeNode>();
                    schemaNodeList.Add(childNode);

                    schemas.Add(schema, schemaNodeList);

                    // create schema folder
                    TreeNode schemaNode;
                    if (Options.CloneParentNode)
                    {
                        schemaNode = new SchemaFolderTreeNode(node);
                    }
                    else
                    {
                        schemaNode = new TreeNode(schema);
                    }
                    schemaNodes.Add(schema, schemaNode);
                    nodeNodes.Add(schemaNode);

                    schemaNode.Name = schema;
                    schemaNode.Text = schema;
                    schemaNode.Tag = nodeTag;

                    if (Options.AppendDot)
                        schemaNode.Text += ".";

                    if (Options.UseObjectIcon)
                    {
                        schemaNode.ImageIndex = childNode.ImageIndex;
                        schemaNode.SelectedImageIndex = childNode.ImageIndex;
                    }
                    else
                    {
                        schemaNode.ImageIndex = node.ImageIndex;
                        schemaNode.SelectedImageIndex = node.ImageIndex;
                    }
                }
            }

            //debug_message("Clear Nodes:{0}", sw.ElapsedMilliseconds);

            //node.TreeView.BeginUpdate();
            node.Text = nodeText + " (clearing...)";
            node.TreeView.Update();
            node.Nodes.Clear();

            //debug_message("DoEvents:{0}", sw.ElapsedMilliseconds);

            if (sw.ElapsedMilliseconds > Options.UnresponsiveTimeout)
            {
                Application.DoEvents();
                if (node.TreeView == null)
                    return 0;
            }

            node.Text = nodeText + " (adding...)";
            node.TreeView.Update();

            //debug_message("Add schemaNode.Nodes:{0}", sw.ElapsedMilliseconds);

            foreach (string schema in schemas.Keys)
            {
                schemaNodes[schema].Nodes.AddRange(schemas[schema].ToArray());
            }

            //debug_message("Add node.Nodes:{0}", sw.ElapsedMilliseconds);

            node.Nodes.AddRange(nodeNodes.ToArray());
            node.Text = nodeText;

            //debug_message("EndUpdate:{0}", sw.ElapsedMilliseconds);

            //node.TreeView.EndUpdate();

            //debug_message("Done:{0}", sw.ElapsedMilliseconds);
            sw.Stop();

            return schemas.Count;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void debug_message(string message, params object[] args)
        {
            if (Package is IDebugOutput)
            {
                ((IDebugOutput)Package).debug_message(message, args);
            }
        }

    }

}
