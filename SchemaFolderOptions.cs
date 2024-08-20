﻿namespace SsmsSchemaFolders
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;

    using Localization;
    using Microsoft.VisualStudio.Shell;
    using System.Windows.Forms;
    using System.Windows.Forms.Design;

    public class SchemaFolderOptions : DialogPage, ISchemaFolderOptions
    {
        [CategoryResources(nameof(SchemaFolderOptions) + "Active")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(Enabled))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(Enabled))]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        [CategoryResources(nameof(SchemaFolderOptions) + "Active")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(EnabledModifierKeys))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(EnabledModifierKeys))]
        [DefaultValue(Keys.Control)]
        //[Editor(typeof(ShortcutKeysEditor), typeof(UITypeEditor))]
        public Keys EnabledModifierKeys { get; set; } = Keys.Control;

        [CategoryResources(nameof(SchemaFolderOptions) + "FolderDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(AppendDot))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(AppendDot))]
        [DefaultValue(true)]
        public bool AppendDot { get; set; } = true;

        [CategoryResources(nameof(SchemaFolderOptions) + "FolderDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(CloneParentNode))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(CloneParentNode))]
        [DefaultValue(true)]
        public bool CloneParentNode { get; set; } = true;

        [CategoryResources(nameof(SchemaFolderOptions) + "FolderDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(UseObjectIcon))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(UseObjectIcon))]
        [DefaultValue(true)]
        public bool UseObjectIcon { get; set; } = true;

        [CategoryResources(nameof(SchemaFolderOptions) + "ObjectDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(RenameNode))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(RenameNode))]
        [DefaultValue(false)]
        public bool RenameNode { get; set; } = false;

        [CategoryResources(nameof(SchemaFolderOptions) + "Performance")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(QuickSchema))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(QuickSchema))]
        [DefaultValue(0)]
        public int QuickSchema { get; set; } = 0;

        [CategoryResources(nameof(SchemaFolderOptions) + "Performance")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(UnresponsiveTimeout))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(UnresponsiveTimeout))]
        [DefaultValue(200)]
        public int UnresponsiveTimeout { get; set; } = 200;

        [CategoryResources(nameof(SchemaFolderOptions) + "Performance")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(UseClear))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(UseClear))]
        [DefaultValue(0)]
        public int UseClear { get; set; } = 0;

        [CategoryResources(nameof(SchemaFolderOptions) + "FolderDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(RegularExpressions))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(RegularExpressions))]
        [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(System.Drawing.Design.UITypeEditor))]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(CsvConverter))]
        public List<string> RegularExpressions { get; set; } = new List<string>();

        [CategoryResources(nameof(SchemaFolderOptions) + "FolderDisplayOptions")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(FolderNameCase))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(FolderNameCase))]
        [DefaultValue(FolderNameCase.Original)]
        public FolderNameCase FolderNameCase { get; set; }

        [CategoryResources(nameof(SchemaFolderOptions) + "Active")]
        [DisplayNameResources(nameof(SchemaFolderOptions) + nameof(EnableDebugOutput))]
        [DescriptionResources(nameof(SchemaFolderOptions) + nameof(EnableDebugOutput))]
        [DefaultValue(false)]
        public bool EnableDebugOutput { get; set; } = false;

        [Browsable(false)]
        [DefaultValue(null)]
        public string RegularExpressionsSerialized
        {
            get
            {
                if (RegularExpressions.Count < 1)
                    return null;
                return string.Join("|", RegularExpressions
                    .Select(_ => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_))));
            }
            set
            {
                if (RegularExpressions == null)
                    RegularExpressions = new List<string>();
                else
                    RegularExpressions.Clear();
                if (value == null)
                {
                    return;
                }
                var base64Elements = value.Split('|');
                foreach (var base64Element in base64Elements)
                {
                    RegularExpressions.Add(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Element)));
                }
            }
        }

        public bool ShouldSerializeRegularExpressions()
        {
            return false;
        }
    }

    public class CsvConverter : TypeConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            List<String> v = value as List<String>;
            if (destinationType == typeof(string))
            {
                return String.Join(",", v.ToArray());
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
