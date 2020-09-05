namespace SsmsSchemaFolders
{
    using System;
    using System.Collections.Generic;
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
                                    return group.Value;
                            }
                        }
                    }
                }
                // Fallback if nothing found by reg expressions
                if (ni.InvariantName.EndsWith("." + ni.Name))
                    return ni.InvariantName.Replace("." + ni.Name, String.Empty);
            }
            return null;
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

            node.TreeView.BeginUpdate();

            //can't move nodes while iterating forward over them
            //create list of nodes to move then perform the update

            var schemas = new Dictionary<String, List<TreeNode>>();

            foreach (TreeNode childNode in node.Nodes)
            {
                //skip schema node folders but make sure they are in schemas list
                if (childNode.Tag != null && childNode.Tag.ToString() == nodeTag)
                {
                    if (!schemas.ContainsKey(childNode.Name))
                        schemas.Add(childNode.Name, new List<TreeNode>());

                    continue;
                }

                var schema = GetNodeSchema(childNode);

                if (string.IsNullOrEmpty(schema))
                    continue;

                //create schema node
                if (!node.Nodes.ContainsKey(schema))
                {
                    TreeNode schemaNode;
                    if (Options.CloneParentNode)
                    {
                        schemaNode = new SchemaFolderTreeNode(node);
                        node.Nodes.Add(schemaNode);
                    }
                    else
                    {
                        schemaNode = node.Nodes.Add(schema);
                    }

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
            }

            //move nodes to schema node
            foreach (string schema in schemas.Keys)
            {
                var schemaNode = node.Nodes[schema];
                foreach (TreeNode childNode in schemas[schema])
                {
                    node.Nodes.Remove(childNode);
                    if (Options.RenameNode)
                    {
                        // Note: Node is renamed back to orginal after expanding.
                        RenameNode(childNode);
                    }
                    schemaNode.Nodes.Add(childNode);
                }
            }

            node.TreeView.EndUpdate();

            return schemas.Count;
        }
    }
}
