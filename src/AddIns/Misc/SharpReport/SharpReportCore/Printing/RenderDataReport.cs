
//
// SharpDevelop ReportEditor
//
// Copyright (C) 2005 Peter Forstmeier
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// Peter Forstmeier (Peter.Forstmeier@t-online.de)

using System;

using System.Data;
using System.Drawing;
using System.ComponentModel;
using System.Globalization;
using System.Drawing.Printing;

using SharpReportCore;

/// <summary>
/// Renderer for DataReports
/// </summary>
/// <remarks>
/// 	created by - Forstmeier Peter
/// 	created on - 13.12.2004 11:07:59
/// </remarks>
/// 

namespace SharpReportCore {
	public class RenderDataReport : AbstractDataRenderer {

		private PointF currentPoint;
		private DataNavigator dataNavigator;

		
		public RenderDataReport(ReportModel model,DataManager dataManager):base (model,dataManager){
//			base.DataManager.GroupChanged += new EventHandler<GroupChangedEventArgs>(OnGroupChanged);
//			base.DataManager.GroupChanging += new EventHandler <EventArgs> (OnGroupChanging);

			
		}
		

		void OnGroupChanged (object sender,GroupChangedEventArgs e) {
			
			System.Console.WriteLine("OnGroupChanged");
		}
		
		void OnGroupChanging (object sender, EventArgs e) {
			
			System.Console.WriteLine("OnGroupChanging");
		}
		
		private void OnListChanged (object sender,System.ComponentModel.ListChangedEventArgs e) {
//			System.Console.WriteLine("List Changed sender <{0}> reason <{1}>",
//			                         sender.ToString(),
//			                         e.ListChangedType);
		}
		
		#region overrides
		
		#region Draw the different report Sections
		
		private void DoReportHeader (ReportPageEventArgs rpea){			
			base.RenderSection (rpea);
			base.DoItems(rpea);
		}
		
		private void DoPageHeader (ReportPageEventArgs rpea){
			this.CurrentSection.SectionOffset = base.Page.PageHeaderRectangle.Location.Y;
			base.RenderSection (rpea);
			base.DoItems(rpea);
		}
		
		
		
		private void DoPageEnd (ReportPageEventArgs rpea){
//			System.Console.WriteLine("DoPageEnd");
			this.CurrentSection.SectionOffset = base.Page.PageFooterRectangle.Location.Y;
			base.RenderSection (rpea);
			base.DoItems(rpea);
		}
		
		//TODO how should we handle ReportFooter, print it on an seperate page ????
		private void  DoReportFooter (PointF startAt,ReportPageEventArgs rpea){
			this.CurrentSection.SectionOffset = (int)rpea.LocationAfterDraw.Y;
			base.RenderSection (rpea);
			base.DoItems(rpea);
		}
		
		private bool IsRoomForFooter(Point loc) {
			Rectangle r =  new Rectangle( base.Page.ReportFooterRectangle.Left,
			                             loc.Y,
			                             base.Page.ReportFooterRectangle.Width,
			                             base.Page.ReportFooterRectangle.Height);
			
			Rectangle s = new Rectangle (base.Page.ReportFooterRectangle.Left,
			                             loc.Y,
			                             
			                             base.Page.ReportFooterRectangle.Width,
			                             base.Page.PageFooterRectangle.Top - loc.Y -1);
			return s.Contains(r);
		}
		
		#endregion

		#region test
		
		protected override void PrintReportHeader (object sender, ReportPageEventArgs e) {
			base.PrintReportHeader (sender,e);
			DoReportHeader (e);
			base.RemoveSectionEvents();
		}
		
		protected override void PrintPageHeader (object sender, ReportPageEventArgs e) {
			base.PrintPageHeader (sender,e);
			DoPageHeader(e);
			base.RemoveSectionEvents();
		}
		
		protected override void PrintPageEnd(object sender, ReportPageEventArgs rpea) {
//			System.Console.WriteLine("DataRenderer:PrintPageEnd");
			base.PrintPageEnd(sender,rpea);
			this.DoPageEnd (rpea);
			base.RemoveSectionEvents();
			
		}
		
		protected override void PrintReportFooter(object sender, ReportPageEventArgs rpea){
//			DebugFooterRectangle(rpea);
			this.CurrentSection.SectionOffset = (int)rpea.LocationAfterDraw.Y;
			base.PrintReportFooter(sender, rpea);
			DoReportFooter (new PointF(0,
			                           base.CurrentSection.SectionOffset + base.CurrentSection.Size.Height),
			                rpea);
			base.RemoveSectionEvents();
		}
		
		protected override void ReportEnd(object sender, PrintEventArgs e){
//			System.Console.WriteLine("DataRenderer:ReportEnd");
			base.ReportEnd(sender, e);
		}
		
		#endregion
		
		
		#region overrides
		
		
		protected override void ReportQueryPage(object sender, QueryPageSettingsEventArgs qpea) {
			base.ReportQueryPage (sender,qpea);
		}
		
		protected override void ReportBegin(object sender, PrintEventArgs pea) {
//			System.Console.WriteLine("");
//			System.Console.WriteLine("ReportBegin (BeginPrint)");
			base.ReportBegin (sender,pea);
			base.DataManager.ListChanged += new EventHandler<ListChangedEventArgs> (OnListChanged);
			dataNavigator = base.DataManager.GetNavigator;
			dataNavigator.ListChanged += new EventHandler<ListChangedEventArgs> (OnListChanged);
			dataNavigator.Reset();
			base.DataNavigator = dataNavigator;
		}
		
		
		
		protected override void BodyStart(object sender, ReportPageEventArgs rpea) {
			System.Console.WriteLine("");
			System.Console.WriteLine("BodyStart");
			base.BodyStart (sender,rpea);
			this.currentPoint = new PointF (base.CurrentSection.Location.X,
			                                base.page.DetailStart.Y);
			
			base.CurrentSection.SectionOffset = (int)this.page.DetailStart.Y + AbstractRenderer.Gap;
			System.Console.WriteLine("\tAdd SectionEvents");
			base.AddSectionEvents();
		}
		
		
		protected override void PrintDetail(object sender, ReportPageEventArgs rpea){
			Rectangle sectionRect;
			bool firstOnPage = true;

			base.PrintDetail(sender, rpea);
			// no loop if there is no data
			if (! this.dataNavigator.HasMoreData ) {
				rpea.PrintPageEventArgs.HasMorePages = false;
				return;
			}
			
			// first element
			if (this.ReportDocument.PageNumber ==1) {
				this.dataNavigator.MoveNext();
			}
			
			do {
				this.dataNavigator.Fill (base.CurrentSection.Items);
				base.RenderSection (rpea);
				
				if (!firstOnPage) {
					base.CurrentSection.SectionOffset = base.CurrentSection.SectionOffset + base.CurrentSection.Size.Height  + 2 * AbstractRenderer.Gap;
					
				}
				
				
				base.FitSectionToItems (base.CurrentSection,rpea.PrintPageEventArgs);
				
				sectionRect = new Rectangle (rpea.PrintPageEventArgs.MarginBounds.Left,
				                             base.CurrentSection.SectionOffset,
				                             rpea.PrintPageEventArgs.MarginBounds.Width,
				                             base.CurrentSection.Size.Height);
				
				if (!base.Page.DetailArea.Contains(sectionRect)) {
					AbstractRenderer.PageBreak(rpea);
					System.Console.WriteLine("DataRenderer:RemoveEvents reason <PageBreak>");
					this.RemoveSectionEvents();
					return;
				}
				
				int i = base.DoItems(rpea);
				this.currentPoint = new PointF (base.CurrentSection.Location.X, i);
				firstOnPage = false;

				if (this.dataNavigator.CurrentRow < this.dataNavigator.Count -1) {
					if (base.CurrentSection.PageBreakAfter) {
						AbstractRenderer.PageBreak(rpea);
						System.Console.WriteLine("DataRenderer:RemoveEvents reason <PageBreakAfter>");
						this.RemoveSectionEvents();
			
						return;
					}
				}
			}
			while (this.dataNavigator.MoveNext());

			this.ReportDocument.DetailsDone = true;
			
			// test for reportfooter
			if (!IsRoomForFooter (rpea.LocationAfterDraw)) {
				AbstractRenderer.PageBreak(rpea);
			}

		}
		
		protected override void BodyEnd(object sender, ReportPageEventArgs rpea) {
			System.Console.WriteLine("");
			System.Console.WriteLine("BodyEnd ");

			base.BodyEnd (sender,rpea);
			System.Console.WriteLine("\tRemoveEvents reason <finish>");
			base.RemoveSectionEvents();

			rpea.PrintPageEventArgs.HasMorePages = false;
		}
		
		
		
		
		#endregion
		
		public override string ToString() {
			base.ToString();
			return "RenderDataReport";
		}
		#endregion

		
	}
}
