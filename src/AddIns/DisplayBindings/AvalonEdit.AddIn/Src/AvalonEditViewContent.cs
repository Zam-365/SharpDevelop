﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Threading;

using ICSharpCode.AvalonEdit.AddIn.Options;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Utils;
using ICSharpCode.Core;
using ICSharpCode.NRefactory.Editor;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Editor.Bookmarks;
using ICSharpCode.SharpDevelop.Editor;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Parser;
using ICSharpCode.SharpDevelop.Project;
using ICSharpCode.SharpDevelop.Workbench;

namespace ICSharpCode.AvalonEdit.AddIn
{
	public class AvalonEditViewContent
		: AbstractViewContent, IMementoCapable, IToolsHost
	{
		readonly CodeEditor codeEditor = new CodeEditor();
		IAnalyticsMonitorTrackedFeature trackedFeature;
		
		public AvalonEditViewContent(OpenedFile file)
		{
			// Use common service container for view content and primary text editor.
			// This makes all text editor services available as view content services and vice versa.
			// (with the exception of the interfaces implemented directly by this class,
			// those are available as view-content services only)
			this.Services = codeEditor.PrimaryTextEditor.GetRequiredService<IServiceContainer>();
			this.TabPageText = "${res:FormsDesigner.DesignTabPages.SourceTabPage}";
			
			if (file.FileName != null) {
				string filetype = Path.GetExtension(file.FileName);
				if (!IsKnownFileExtension(filetype))
					filetype = ".?";
				trackedFeature = SD.AnalyticsMonitor.TrackFeature(typeof(AvalonEditViewContent), "open" + filetype.ToLowerInvariant());
			}
			
			this.Files.Add(file);
			this.LoadModel();
		}
		
		bool IsKnownFileExtension(string filetype)
		{
			return ProjectService.GetFileFilters().Any(f => f.ContainsExtension(filetype)) ||
				IconService.HasImageForFile(filetype);
		}
		
		public override object Control {
			get { return codeEditor; }
		}
		
		public override object InitiallyFocusedControl {
			get { return codeEditor.PrimaryTextEditor.TextArea; }
		}
		
		
		public override void LoadModel()
		{
			// if (!file.IsUntitled) {
			//    codeEditor.PrimaryTextEditor.IsReadOnly = (File.GetAttributes(file.FileName) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
			// }
			
			codeEditor.Document = PrimaryFile.GetModel(FileModels.TextDocument);
			base.LoadModel();
			
			// we set the file name after loading because this will place the fold markers etc.
			codeEditor.FileName = PrimaryFile.FileName;
			BookmarksAttach();
		}
		
		protected override void OnFileNameChanged(OpenedFile file)
		{
			base.OnFileNameChanged(file);
			if (file == PrimaryFile) {
				FileName oldFileName = codeEditor.FileName;
				FileName newFileName = file.FileName;
				
				if (!string.IsNullOrEmpty(oldFileName))
					SD.ParserService.ClearParseInformation(oldFileName);
				
				
				BookmarksNotifyNameChange(oldFileName, newFileName);
				// changing the filename on the codeEditor raises several events; ensure
				// we got our state updated first (bookmarks, persistent anchors) before other code
				// processes the file name change
				
				codeEditor.FileName = newFileName;
				
				SD.ParserService.ParseAsync(file.FileName, codeEditor.Document).FireAndForget();
			}
		}
		
		public override INavigationPoint BuildNavPoint()
		{
			return codeEditor.BuildNavPoint();
		}
		
		public override bool IsReadOnly {
			get { return codeEditor.PrimaryTextEditor.IsReadOnly; }
		}
		
		#region Bookmark Handling
		bool bookmarksAttached;
		
		void BookmarksAttach()
		{
			if (bookmarksAttached) return;
			bookmarksAttached = true;
			foreach (SDBookmark bookmark in SD.BookmarkManager.GetBookmarks(codeEditor.FileName)) {
				bookmark.Document = codeEditor.Document;
				codeEditor.IconBarManager.Bookmarks.Add(bookmark);
			}
			SD.BookmarkManager.BookmarkAdded += BookmarkManager_Added;
			SD.BookmarkManager.BookmarkRemoved += BookmarkManager_Removed;
			
			PermanentAnchorService.AttachDocument(codeEditor.FileName, codeEditor.Document);
		}
		
		void BookmarksDetach()
		{
			if (codeEditor.FileName != null) {
				PermanentAnchorService.DetachDocument(codeEditor.FileName, codeEditor.Document);
			}
			
			SD.BookmarkManager.BookmarkAdded -= BookmarkManager_Added;
			SD.BookmarkManager.BookmarkRemoved -= BookmarkManager_Removed;
			foreach (SDBookmark bookmark in codeEditor.IconBarManager.Bookmarks.OfType<SDBookmark>()) {
				if (bookmark.Document == codeEditor.Document) {
					bookmark.Document = null;
				}
			}
			codeEditor.IconBarManager.Bookmarks.Clear();
		}
		
		void BookmarkManager_Removed(object sender, BookmarkEventArgs e)
		{
			codeEditor.IconBarManager.Bookmarks.Remove(e.Bookmark);
			if (e.Bookmark.Document == codeEditor.Document) {
				e.Bookmark.Document = null;
			}
		}
		
		void BookmarkManager_Added(object sender, BookmarkEventArgs e)
		{
			if (FileUtility.IsEqualFileName(this.PrimaryFileName, e.Bookmark.FileName)) {
				codeEditor.IconBarManager.Bookmarks.Add(e.Bookmark);
				e.Bookmark.Document = codeEditor.Document;
			}
		}
		
		void BookmarksNotifyNameChange(FileName oldFileName, FileName newFileName)
		{
			PermanentAnchorService.RenameDocument(oldFileName, newFileName, codeEditor.Document);
			
			foreach (SDBookmark bookmark in codeEditor.IconBarManager.Bookmarks.OfType<SDBookmark>()) {
				bookmark.FileName = newFileName;
			}
		}
		#endregion
		
		public override void Dispose()
		{
			if (trackedFeature != null)
				trackedFeature.EndTracking();
			base.Dispose();
			BookmarksDetach();
			codeEditor.Dispose();
		}
		
		public override string ToString()
		{
			return "[" + GetType().Name + " " + this.PrimaryFileName + "]";
		}
		
		#region IMementoCapable
		public Properties CreateMemento()
		{
			Properties memento = new Properties();
			memento.Set("CaretOffset", codeEditor.ActiveTextEditor.CaretOffset);
			memento.Set("ScrollPositionY", codeEditor.ActiveTextEditor.VerticalOffset);
			return memento;
		}
		
		public void SetMemento(Properties memento)
		{
			codeEditor.PrimaryTextEditor.ScrollToVerticalOffset(memento.Get("ScrollPositionY", 0.0));
			try {
				codeEditor.PrimaryTextEditor.CaretOffset = memento.Get("CaretOffset", 0);
			} catch (ArgumentOutOfRangeException) {
				// ignore caret out of range - maybe file was changed externally?
			}
		}
		#endregion
		
		object IToolsHost.ToolsContent {
			get { return TextEditorSideBar.Instance; }
		}
	}
}
