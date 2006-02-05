//------------------------------------------------------------------------------
// <autogenerated>
//     This code was generated by a tool.
//     Runtime Version: 1.1.4322.2032
//
//     Changes to this file may cause incorrect behavior and will be lost if 
//     the code is regenerated.
// </autogenerated>
//------------------------------------------------------------------------------


using System;
using System.Drawing;
using System.ComponentModel;
using System.Drawing.Printing;

using SharpReportCore;	
using SharpReport.Designer;
	/// <summary>
	/// This Function print's the PageNumber
	/// Localise it by just overriding the Text Property
	/// </summary>
	/// <remarks>
	/// 	created by - Forstmeier Peter
	/// 	created on - 19.04.2005 22:23:48
	/// </remarks>
namespace SharpReport.ReportItems.Functions {
	
		public class PageNumber : SharpReportCore.BasePageNumber,SharpReport.Designer.IDesignable {
		private string functionName = "PageNumber";
		private FunctionControl visualControl;
		private bool initDone;
		
		public new event PropertyChangedEventHandler PropertyChanged;
		
		public PageNumber():base(){
			visualControl = new FunctionControl();		
			
			this.visualControl.Click += new EventHandler(OnControlSelect);
			this.visualControl.VisualControlChanged += new EventHandler (OnControlChanged);
			this.visualControl.BackColorChanged += new EventHandler (OnControlChanged);
			this.visualControl.FontChanged += new EventHandler (OnControlChanged);
			this.visualControl.ForeColorChanged += new EventHandler (OnControlChanged);
			
			base.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler (BasePropertyChange);

			ItemsHelper.UpdateTextControl (this.visualControl,this);

			this.Text = functionName;
			this.visualControl.FunctionValue = String.Empty;
			GrapFromBase();
			this.initDone = true;
		}
		private void tt (object sender, PropertyChangedEventArgs e) {
			
		}
		private void GrapFromBase() {
			this.visualControl.SuspendLayout();
			visualControl.StringFormat = base.StandartStringFormat;
			this.visualControl.ResumeLayout();
		}
		
		
		private void BasePropertyChange (object sender, PropertyChangedEventArgs e){
			if (initDone == true) {
				ItemsHelper.UpdateTextBase(this.visualControl,this);
			}
		}
		

		private void OnControlChanged (object sender, EventArgs e) {
			ItemsHelper.UpdateTextControl (this.visualControl,this);
			this.HandlePropertyChanged("OnControlChanged");
		}
		
		public void OnControlSelect(object sender, EventArgs e){
			if (Selected != null)
				Selected(this,e);
		}	
		
		/// <summary>
		/// A Property in ReportItem has changed, inform the Designer
		/// to set the View's 'IsDirtyFlag' to true
		/// </summary>
		
		protected void HandlePropertyChanged(string info) {
			if ( !base.Suspend) {
				if (PropertyChanged != null) {
					PropertyChanged (this,new PropertyChangedEventArgs(info));
				}
			}
		}
		
		#region Properties
		public override Size Size {
			get {
				return base.Size;
			}
			set {
				base.Size = value;
				if (this.visualControl != null) {
					this.visualControl.Size = value;
				}
				this.HandlePropertyChanged("Size");
			}
		}
		
		public override Point Location {
			get {
				return base.Location;
			}
			set {
				base.Location = value;
				if (this.visualControl != null) {
					this.visualControl.Location = value;
				}
				this.HandlePropertyChanged("Location");
			}
		}
		
		public override Font Font {
			get {
				return base.Font;
			}
			set {
				base.Font = value;
				if (this.visualControl != null) {
					this.visualControl.Font = value;
				}
				this.HandlePropertyChanged("Font");
			}
		}
		///<summary>
		/// Holds the text to be displayed
		/// </summary>
		
		public override string Text{
			get {
				return base.Text;
			}
			set {
				base.Text = value;
				if (this.visualControl.Text != value) {
					this.visualControl.Text = value;
					this.visualControl.Refresh();
				}
				this.HandlePropertyChanged("Text");
			}
		}
		
		#endregion
		
		#region IDesignable
		
		[System.Xml.Serialization.XmlIgnoreAttribute]
		[Browsable(false)]		
		public ReportObjectControlBase VisualControl {
			get {
				return visualControl;
			}
		}
		
		public event EventHandler <EventArgs> Selected;

		#endregion
	}
}
