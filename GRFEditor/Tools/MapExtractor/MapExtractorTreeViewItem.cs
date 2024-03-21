﻿using System;
using System.Windows;
using System.Windows.Controls;
using TokeiLibrary;
using TokeiLibrary.WPF;
using TokeiLibrary.WPF.Styles.ListView;
using Utilities;

namespace GRFEditor.Tools.MapExtractor {
	public class MapExtractorTreeViewItem : TkTreeViewItem {
		private TkPath _resourcePath;

		public MapExtractorTreeViewItem(TkView parent) : base(parent, false) {
			CanBeDragged = true;
			UseCheckBox = true;
			CheckBoxHeaderIsEnabled = true;

			Style = (Style)FindResource("MapExtractorTreeViewItemStyle");
		}

		public TkPath ResourcePath {
			get { return _resourcePath; }
			set {
				_resourcePath = value;

				if (_resourcePath != null)
					ToolTip = "GRF: " + _resourcePath.FilePath + (String.IsNullOrEmpty(_resourcePath.RelativePath) ? "" : "\r\n" + _resourcePath.RelativePath);
				else {
					ToolTip = "Resource not found.";
				}
			}
		}
	}
}