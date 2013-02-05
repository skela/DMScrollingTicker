using System;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreAnimation;
using MonoTouch.ObjCRuntime;

namespace Ticker.UI
{
	public enum ScrollDirection
	{
		FromLeft=0,
		FromRight=1
	}

	public delegate void ScrollingTickerAnimationCompletition(uint loopsDone,bool isFinished);
	public delegate UIView ScrollingTickerLazyLoadingHandler(uint indexOfViewToShow);
	public delegate void ScrollingTickerTappedSubview(UIView subview);

	public class TickerView : UIView
	{
		private bool isAnimating;            // YES if an animation is currently in progress
		private uint loopsDone;              // Number of loops made since the beginning of the animation
		private int numberOfLoops;          // number of loops to make (0 means it will go on forever)
		private ScrollDirection scrollViewDirection;                // scroll direction of the ticker
		private float scrollViewSpeed;         

		private UIScrollView scrollView;
		private List<NSObject> tickerSubViews;                     // preloaded subviews (if any)
		private List<NSValue> tickerSubviewsFrames;   

		private CADisplayLink displayLink;

		// Block handlers
		ScrollingTickerLazyLoadingHandler         lazyLoadingHandler;
		ScrollingTickerAnimationCompletition      animationCompletitionHandler;
		ScrollingTickerTappedSubview tapHandler;

		// Constants
		float kLPScrollingTickerHSpace = 2.0f;
		float kLPScrollingAnimationPixelsPerSecond = 50.0f; // Default animation speed

		public TickerView (RectangleF frame) : base(frame)
		{
			scrollViewDirection = ScrollDirection.FromRight;
			numberOfLoops=0;
			isAnimating=false;
			tickerSubViews = null;
			tickerSubviewsFrames = null;

			scrollView = new UIScrollView(Bounds);
			scrollView.ShowsHorizontalScrollIndicator = false;
			scrollView.ShowsVerticalScrollIndicator = false;
			scrollView.ScrollEnabled = false;

			AddSubview(scrollView);
			BackgroundColor = UIColor.Clear;
			scrollView.BackgroundColor = UIColor.Clear;
		}

		public override void MovedToSuperview()
		{
			base.MovedToSuperview();
	
			StartListeningForApplicationState();
		}
		
		public override void RemoveFromSuperview()
		{
			base.RemoveFromSuperview();

			StopListeningForApplicationState();
		}

		public void BeginAnimationWithViews (List<UIView> views, ScrollDirection direction=ScrollDirection.FromRight, float scrollSpeed=0.0f, int loops=0,ScrollingTickerAnimationCompletition completition=null)
		{
			if (isAnimating) EndAnimation(false);				
			
			lazyLoadingHandler = null;
			animationCompletitionHandler = completition;
			numberOfLoops = loops;
			scrollViewDirection = direction;
			scrollViewSpeed = (scrollSpeed == 0 ? kLPScrollingAnimationPixelsPerSecond : scrollSpeed);
			
			if (displayLink!=null) 
			{
				// Display link is used to catch the current visible area of the scrolling view during the animation
				displayLink.RemoveFromRunLoop(NSRunLoop.Main,NSRunLoop.NSDefaultRunLoopMode);
				displayLink = null;
			}

			LayoutTickerSubviewsWithItems(views);
			BeginAnimation();
		}

		public void BeginAnimationWithLazyViews (ScrollingTickerLazyLoadingHandler dataSource,List<NSValue>subviewsSizes,ScrollDirection direction=ScrollDirection.FromRight, float scrollSpeed=0.0f, int loops=0,ScrollingTickerAnimationCompletition completition=null)
		{
			if (isAnimating) EndAnimation(false);				
			
			lazyLoadingHandler = dataSource;
			animationCompletitionHandler = completition;
			numberOfLoops = loops;
			scrollViewDirection = direction;
			scrollViewSpeed = (scrollSpeed == 0 ? kLPScrollingAnimationPixelsPerSecond : scrollSpeed);

			displayLink = CADisplayLink.Create(this,new Selector("tickerDidScroll"));
			displayLink.AddToRunLoop(NSRunLoop.Main,NSRunLoop.NSDefaultRunLoopMode);

			LayoutTickerSubviewsWithItemSizes(subviewsSizes);
			BeginAnimation();
		}

		public void PauseAnimation()
		{
			if (!isAnimating) return;
			isAnimating = false;
			PauseLayer(scrollView.Layer);
		}
		
		public void ResumeAnimation()
		{
			if (isAnimating) return;
			isAnimating = true;
			ResumeLayer(scrollView.Layer);
		}

		public void Clear()
		{
			EndAnimation(false);

			foreach (UIView v in scrollView.Subviews)
			{
				if (!(v is UIImageView))
				{
					v.RemoveFromSuperview();
				}
			}
		}

		#region Tapping

		public ScrollingTickerTappedSubview TapDelegate
		{
			set
			{
				tapHandler = value;
				if (tapRecognizer==null)
				{
					tapRecognizer = new UITapGestureRecognizer();
					tapRecognizer.AddTarget( () => TappedTickerView(tapRecognizer) );
					AddGestureRecognizer(tapRecognizer);
				}
			}
			get
			{
				return tapHandler;
			}
		}

		UITapGestureRecognizer tapRecognizer;

		RectangleF tempFrame;
		public void TappedTickerView(UITapGestureRecognizer recog)
		{
			if (tapHandler==null)
				return;

			PointF point = recog.LocationInView(this);
			point.X = point.X + VisibleContentRect.X;
			UIView tappedView = null;
			Console.WriteLine("point is " + point.ToString());
			foreach (UIView v in scrollView.Subviews)
			{
				if (!(v is UIImageView))
				{
					Console.WriteLine("v with tag {0} and frame {1}",v.Tag,v.Frame.ToString());
					tempFrame = v.Frame;
					tempFrame.Height = Bounds.Height;
					if (tempFrame.Contains(point))
					{
						tappedView = v;
						break;
					}
				}
			}

			if (tapHandler!=null && tappedView!=null)
				tapHandler(tappedView);
		}

		#endregion

		#region Application State

		bool wasAnimating;

		public void ApplicationDidEnterBackground()
		{
			wasAnimating = isAnimating;
			PauseAnimation();
		}
		
		public void ApplicationWillEnterForeground()
		{
			if (wasAnimating)
			{
				BeginAnimation();
				ResumeLayer(scrollView.Layer);
			}
		}
		
		private void ApplicationDidEnterBackground(NSNotification n)
		{
			ApplicationDidEnterBackground();
		}
		
		private void ApplicationWillEnterForeground(NSNotification n)
		{
			ApplicationWillEnterForeground();	
		}
		
		private void StopListeningForApplicationState()
		{			
			NSNotificationCenter.DefaultCenter.RemoveObserver(this,UIApplication.DidEnterBackgroundNotification,null);
			NSNotificationCenter.DefaultCenter.RemoveObserver(this,UIApplication.WillEnterForegroundNotification,null);
		}
		
		private void StartListeningForApplicationState()
		{
			StopListeningForApplicationState();
			NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidEnterBackgroundNotification,ApplicationDidEnterBackground);
			NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillEnterForegroundNotification,ApplicationWillEnterForeground);		
		}

		#endregion

		#region Internal

		PointF StartOffset 
		{
			get
			{
				PointF startOffset = new PointF();
				if (scrollViewDirection == ScrollDirection.FromRight)
					startOffset = new PointF(-scrollView.Frame.Size.Width, 0);
				else if (scrollViewDirection == ScrollDirection.FromLeft)
					startOffset = new PointF(scrollView.ContentSize.Width, 0);
				return startOffset;
			}
		}

		private void BeginAnimation()
		{
			if (isAnimating) return;

			scrollView.ContentOffset = StartOffset;

			isAnimating = true;

			double animationDuration = (scrollView.ContentSize.Width/scrollViewSpeed);

			UIView.AnimateNotify(animationDuration,0.0f,UIViewAnimationOptions.CurveLinear|UIViewAnimationOptions.AllowUserInteraction,
			delegate
			{
				PointF finalPoint = new PointF();
				
				if (scrollViewDirection == ScrollDirection.FromRight)
					finalPoint = new PointF(scrollView.ContentSize.Width, 0);
				else if (scrollViewDirection == ScrollDirection.FromLeft)
					finalPoint = new PointF(-scrollView.ContentSize.Width+scrollView.Frame.Size.Width, 0);
				
				scrollView.ContentOffset = finalPoint;
			},
			delegate(bool finished)
			{
				if (finished) 
				{
					isAnimating = false;
					bool restartAnimation = (numberOfLoops == 0 || loopsDone <= numberOfLoops);

					if (animationCompletitionHandler!=null)
						animationCompletitionHandler((loopsDone+1),!restartAnimation);
					
					if (restartAnimation)
						BeginAnimation();
					else
						EndAnimation(false);						
					
					loopsDone++;
				}
			});
		}

		private void EndAnimation(bool animated)
		{
			if (!isAnimating) return;
			isAnimating = false;
			loopsDone = 0;

			PauseLayer(scrollView.Layer);
			ScrollToStart(animated);
		}

		private void ScrollToOffset(PointF offsetPoint,bool animated)
		{
			EndAnimation(false);
			scrollView.SetContentOffset(offsetPoint,animated);
		}
		
		private void ScrollToStart(bool animated)
		{
			EndAnimation(false);
			scrollView.SetContentOffset(StartOffset,animated);
		}

		#endregion

		#region Layout

		SizeF ContentSize 
		{
			get
			{
				return scrollView.ContentSize;
			}
			set
			{
				scrollView.ContentSize = value;
			}
		}

		RectangleF VisibleContentRect 
		{
			get
			{
				RectangleF visibleRect;
				// it returns the correct value while the scrollview is animating (simple scrollView.contentOffset will return a wrong value)
				visibleRect.Location = scrollView.Layer.PresentationLayer.Bounds.Location;					
				visibleRect.Size = scrollView.Frame.Size;
				return visibleRect;
			}
		}

		void LayoutTickerSubviewsWithItemSizes(List<NSValue> frameSizes)
		{
			tickerSubViews = new List<NSObject>();
			tickerSubviewsFrames = new List<NSValue>();
			
			SizeF scrollingContentSize = new SizeF();
			
			float offsetX = 0.0f;
			foreach (NSValue itemSize in frameSizes) 
			{
				RectangleF itemFrame = new RectangleF(offsetX,0,itemSize.SizeFValue.Width,itemSize.SizeFValue.Height);
				tickerSubviewsFrames.Add(NSValue.FromRectangleF(itemFrame));
				tickerSubViews.Add(NSNull.Null);

				float itemWidth = (itemSize.SizeFValue.Width+kLPScrollingTickerHSpace);
				scrollingContentSize.Width +=+itemWidth;
				scrollingContentSize.Height = Math.Max(scrollingContentSize.Height,itemSize.SizeFValue.Height);
				
				offsetX += itemWidth;
			}
			scrollView.ContentSize = scrollingContentSize;
		}
		
		void LayoutTickerSubviewsWithItems(List<UIView>itemsToLoad)
		{
			tickerSubViews = null;
			tickerSubviewsFrames = new List<NSValue>();
			
			SizeF scrollingContentSize = new SizeF();    
			float offsetX = 0.0f;
			foreach (UIView itemView in itemsToLoad) 
			{
				itemView.LayoutSubviews();
				RectangleF itemFrame = new RectangleF(offsetX,0,itemView.Frame.Size.Width,itemView.Frame.Size.Height);
				tickerSubviewsFrames.Add(NSValue.FromRectangleF(itemFrame));

				// calculate content size
				float itemWidth = (itemView.Frame.Size.Width+kLPScrollingTickerHSpace);
				scrollingContentSize.Width +=+itemWidth;
				scrollingContentSize.Height = Math.Max(scrollingContentSize.Height,itemView.Frame.Size.Height);
				offsetX += itemWidth;
				
				itemView.Frame = itemFrame;
				scrollView.AddSubview(itemView);
			}
			scrollView.ContentSize = scrollingContentSize;
		}

		#endregion

		#region IBActions

		[Export("tickerDidScroll")]
		public void TickerDidScroll() 
		{
			// This method is used by lazy loading in order to check and load visible subviews and
			// remove the unused/not visible subviews.
			// This is not called when data loading mode = LPScrollingTickerDataLoading_PreloadSubviews
			uint k = 0;
			RectangleF visibleRect = VisibleContentRect;
			foreach (NSValue itemFrame in tickerSubviewsFrames) 
			{
				bool isVisible = visibleRect.IntersectsWith(itemFrame.RectangleFValue);
				UIView targetView = lazyLoadingHandler(k);				
				// this item will be now visible so we want to allocate it and insert into the subview
				if (isVisible && targetView.Superview == null) 
				{
					targetView.Frame = itemFrame.RectangleFValue;	
					scrollView.AddSubview(targetView);
				} 
				else if (isVisible == false && targetView.Superview != null) 
				{
					// item is not out of the visilble area so we can remove it/dealloc
					targetView.RemoveFromSuperview();
				}
				++k;
			}
		}

		#endregion

		#region Layers

		void PauseLayer(CALayer layer)
		{
			double pausedTime = layer.ConvertTimeFromLayer(CAAnimation.CurrentMediaTime(),null);				
			layer.Speed = 0.0f;				
			layer.TimeOffset = pausedTime;
		}
		
	 	void ResumeLayer(CALayer layer)
		{
			double pausedTime = layer.TimeOffset;				
			layer.Speed = 1.0f;
			layer.TimeOffset = 0.0f;
			layer.BeginTime = 0.0f;
			double timeSincePause = layer.ConvertTimeFromLayer(CAAnimation.CurrentMediaTime(),null) - pausedTime;
			layer.BeginTime = timeSincePause;
		}

		#endregion
	}

	public class TickerLabel : UIView
	{
		private UILabel titleLabel;
		private UILabel descriptionLabel;

		private float kLPScrollingTickerLabelItem_Space = 5.0f;

		public TickerLabel(String title,String description,float height) : base(new RectangleF(0,0,0,height))
		{		
			titleLabel = new UILabel(new RectangleF());				
			titleLabel.Font = UIFont.BoldSystemFontOfSize(14.0f);
			titleLabel.LineBreakMode = UILineBreakMode.WordWrap;
			titleLabel.Lines = 1;
			
			descriptionLabel =  new UILabel(new RectangleF());	
			descriptionLabel.Font = UIFont.SystemFontOfSize(14.0f);
			descriptionLabel.LineBreakMode = UILineBreakMode.WordWrap;
			descriptionLabel.Lines = 1;
			
			descriptionLabel.BackgroundColor = UIColor.Clear;
			titleLabel.BackgroundColor = UIColor.Clear;
			BackgroundColor = UIColor.Clear;

			AddSubview(titleLabel);
			AddSubview(descriptionLabel);

			titleLabel.Text = title;
			descriptionLabel.Text = description;

			LayoutSubviewsNow();
		}

		public override bool UserInteractionEnabled
		{
			get
			{
				return base.UserInteractionEnabled;
			}
			set
			{
				base.UserInteractionEnabled = value;
				titleLabel.UserInteractionEnabled = value;
				descriptionLabel.UserInteractionEnabled = value;
			}
		}

		public override String Description
		{
			get
			{
				return String.Format("text='{0},{1}' frame={2}",titleLabel.Text,descriptionLabel.Text,this.Frame.ToString ());					
			}
		}

		public void LayoutSubviewsNow()
		{
			SizeF bestSize_title = titleLabel.StringSize(titleLabel.Text,titleLabel.Font,new SizeF(float.MaxValue,Frame.Size.Height),UILineBreakMode.WordWrap);
			SizeF bestSize_subtitle = descriptionLabel.StringSize(descriptionLabel.Text,descriptionLabel.Font,new SizeF(float.MaxValue,Frame.Size.Height),UILineBreakMode.WordWrap);
			titleLabel.Frame = new RectangleF(5.0f,0.0f,bestSize_title.Width,Frame.Size.Height);				
			descriptionLabel.Frame = new RectangleF(titleLabel.Frame.X+titleLabel.Frame.Size.Width+kLPScrollingTickerLabelItem_Space,0,bestSize_subtitle.Width,Frame.Size.Height);
			Frame = new RectangleF(Frame.X,Frame.Y,bestSize_title.Width+kLPScrollingTickerLabelItem_Space+bestSize_subtitle.Width+10,Math.Max(bestSize_title.Height,bestSize_subtitle.Height));
		}

		public void LayoutSubviews()
		{
			base.LayoutSubviews();

			LayoutSubviewsNow();
		}

	}
}

