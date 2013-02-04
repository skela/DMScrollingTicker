using System;
using System.Collections.Generic;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using Ticker.UI;

namespace TickerViewTest
{
	public partial class MainViewController : UIViewController
	{
		static bool UserInterfaceIdiomIsPhone {
			get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
		}

		UIPopoverController flipsidePopoverController;
		
		public MainViewController ()
			: base (UserInterfaceIdiomIsPhone ? "MainViewController_iPhone" : "MainViewController_iPad" , null)
		{
			// Custom initialization
		}

		UIButton pauseButton;
		UIButton resumeButton;

		void ClickedPause(Object obj,System.EventArgs e)
		{
			tickerView.PauseAnimation();
		}

		void ClickedResume(Object obj,System.EventArgs e)
		{
			tickerView.ResumeAnimation();
		}

		public void LoadButtons()
		{
			pauseButton = new UIButton(UIButtonType.RoundedRect); pauseButton.Frame = new RectangleF(78,291,180,40); pauseButton.SetTitle("Pause",UIControlState.Normal);
			resumeButton = new UIButton(UIButtonType.RoundedRect); resumeButton.Frame = new RectangleF(78,346,180,40); resumeButton.SetTitle("Resume",UIControlState.Normal);

			pauseButton.AddTarget(ClickedPause,UIControlEvent.TouchUpInside);
			resumeButton.AddTarget(ClickedResume,UIControlEvent.TouchUpInside);

			View.AddSubview(pauseButton);
			View.AddSubview(resumeButton);
		}

		TickerView tickerView;
		public void LoadPicker()
		{
			tickerView = new TickerView(new RectangleF(0,170,320,18));				
			tickerView.BackgroundColor = UIColor.Yellow;
			View.AddSubview(tickerView);
			
			List<UIView> l = new List<UIView>();
			List<NSValue> sizes = new List <NSValue>();
			
			for (uint k = 0; k < 5; k++) 
			{
				TickerLabel label = new TickerLabel(String.Format("> Title {0}",k),String.Format("Description {0}",k),18);
				sizes.Add(NSValue.FromSizeF(label.Frame.Size));
				l.Add(label);
			}
			
			tickerView.BeginAnimationWithViews(l,direction:ScrollDirection.FromRight,scrollSpeed:0.0f,loops:2,completition:delegate(uint loopsDone,bool isFinished)
			                                   {
				Console.WriteLine("loop {0}, finished? {1}",loopsDone,isFinished);
			});

		}

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			LoadButtons();

			LoadPicker();
		}
		
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			// Return true for supported orientations
			if (UserInterfaceIdiomIsPhone)
			{
				return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
			}
			else
			{
				return true;
			}
		}
		
		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}
		
		public override void ViewDidUnload ()
		{
			base.ViewDidUnload ();
			
			// Clear any references to subviews of the main view in order to
			// allow the Garbage Collector to collect them sooner.
			//
			// e.g. myOutlet.Dispose (); myOutlet = null;
			
			ReleaseDesignerOutlets ();
		}
		


	}
}

