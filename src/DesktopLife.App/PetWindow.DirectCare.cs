using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopLife.Core;
using DesktopLife.Windows;

namespace DesktopLife.App;

public partial class PetWindow
{
    private readonly DirectCareSession directCareSession=new();
    private readonly HoverStrokeGate hoverStroke=new();
    private readonly CombStrokeGate combStroke=new();
    private readonly Image hoverHand=new(){IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private static SpriteAssetFrame? handAsset;
    private double directHoldUntil,directAffectionUntil;
    private bool hoveringHead,nearPreviously,physicalCareEvidence;
    private Guid? activeComb;
    internal Func<DirectCareContact,bool>? DirectCareContactRequested {get;set;}
    internal bool DirectCareHolding=>clock.Elapsed.TotalSeconds<directHoldUntil&&DirectCareBlocks()==DirectCareBlock.None;
    internal int DirectPetContacts {get;private set;}
    internal int DirectGroomContacts {get;private set;}
    private void InitializeDirectCareVisual()
    {
        handAsset??=new SpriteAssetReader("Interactions/petting-hand-v1.png",512).Extract([0,0,1536,1024]);
        hoverHand.Source=handAsset.Image;RenderOptions.SetBitmapScalingMode(hoverHand,BitmapScalingMode.HighQuality);
        Surface.Children.Add(hoverHand);
    }
    internal void CancelDirectCare()
    {
        RestoreTeaserLength();
        directCareSession.Reset();hoverStroke.Reset();combStroke.Reset();nearPreviously=hoveringHead=false;
        physicalCareEvidence=false;activeComb=null;directHoldUntil=directAffectionUntil=0;hoverHand.Visibility=Visibility.Collapsed;
        if(worldOwner is null)foreach(var f in Furniture)f.CancelCombDrag();
    }
    private DirectCareBlock DirectCareBlocks()
    {
        var block=DirectCareBlock.None;
        if(!IsVisible)block|=DirectCareBlock.Hidden;
        if(paused)block|=DirectCareBlock.Paused;
        if(EditingRoom)block|=DirectCareBlock.Editing;
        if(Interacting)block|=DirectCareBlock.Dragging;
        if(body.Action is BodyAction.Sleep or BodyAction.RestInCorner||sequence?.Kind==SequenceKind.Sleep)block|=DirectCareBlock.Sleeping;
        if(IsOnStair||FinishingMotion||!petGravity.Grounded||sprite.IsStandingUp||SequenceCommitted)block|=DirectCareBlock.Stairs;
        return block;
    }
    private Point LocalCarePoint(Point room)=>new((room.X-PresentedOrigin.X)*116/BodyWidth,(room.Y-PresentedOrigin.Y)*144/BodyHeight);
    private Rect HeadRegion
    {
        get
        {
            var r=sprite.DrawnBounds;
            return new(r.X+r.Width*.16,r.Y,r.Width*.68,r.Height*(appearance==PetAppearance.Girl?.30:.56));
        }
    }
    private bool PaintedCarePoint(Point room,bool headOnly)
    {
        var local=LocalCarePoint(room);
        if(sprite.Visibility!=Visibility.Visible||!sprite.Ready||!sprite.PaintedAt(local))return false;
        var head=HeadRegion;
        if(headOnly)return head.Contains(local);
        if(appearance!=PetAppearance.Girl)return local.Y<=sprite.DrawnBounds.Y+sprite.DrawnBounds.Height*.78;
        return head.Contains(local)&&(local.Y<head.Y+head.Height*.35||local.X<head.X+head.Width*.28||local.X>head.Right-head.Width*.28);
    }
    private bool ExposedCarePoint(Point room,nint ignoredTool=0)
    {
        var physical=DisplayWorkspace.ToPixels(room);
        bool Receiving(nint hwnd)
        {
            var owner=worldOwner??this;
            foreach(var actor in owner.sharedCharacters.Prepend(owner))
                if(new WindowInteropHelper(actor).Handle==hwnd)return actor.sprite.Visibility==Visibility.Visible&&actor.sprite.PaintedAt(actor.LocalCarePoint(room));
            foreach(var furniture in Furniture)if(furniture.NativeHandle==hwnd)return furniture.ReceivesPointerAt(room);
            return true;
        }
        return PointerExposure.IsExposed(new WindowInteropHelper(this).Handle,physical.X,physical.Y,ignoredTool,Receiving);
    }
    private bool TickDirectCare(double dt)
    {
        if(TryGetHomeStressHoverPointer(out var stressPointer)) return StepDirectCare(stressPointer,dt,null);
        var raw=DesktopInteraction.Cursor();
        Point? pointer=raw is {} p?DisplayWorkspace.FromPixels(new Point(p.X,p.Y)):null;
        return StepDirectCare(pointer,dt,null);
    }
    // Diagnostics may supply a pointer sample, but exercise this same exposure,
    // silhouette, state and motion path. A caller cannot simply grant a reward.
    private bool StepDirectCare(Point? pointer,double dt,RoomWindow? diagnosticComb)
    {
        var blocked=DirectCareBlocks();
        var held=diagnosticComb??Furniture.FirstOrDefault(f=>f.IsCombTool&&f.CombHeld&&f.IsVisible);
        if(held is not null&&PointerExposure.EscapeDown()){held.CancelCombDrag();held=null;}
        if(activeComb!=held?.CombDragId){combStroke.Reset();activeComb=held?.CombDragId;}
        hoveringHead=false;
        if(held is not null)
        {
            hoverStroke.Reset();nearPreviously=false;hoverHand.Visibility=Visibility.Collapsed;
            var tip=held.CombTip;var touching=held.CombHeld&&PaintedCarePoint(tip,false)&&ExposedCarePoint(tip,held.NativeHandle);
            if(combStroke.Update(appearance,tip.X,tip.Y,dt,touching,blocked))DeliverDirectCare(CareKind.Groom);
            if(touching&&blocked==DirectCareBlock.None)directHoldUntil=clock.Elapsed.TotalSeconds+.3;
        }
        else
        {
            combStroke.Reset();
            var point=pointer??new(double.NaN,double.NaN);
            var head=HeadRegion;head.Inflate(8,6);
            var near=pointer is not null&&head.Contains(LocalCarePoint(point))&&blocked==DirectCareBlock.None&&ExposedCarePoint(point);
            var contact=near&&PaintedCarePoint(point,true);
            hoveringHead=near;
            if(near&&!nearPreviously)directHoldUntil=clock.Elapsed.TotalSeconds+1.2;
            nearPreviously=near;
            if(hoverStroke.Update(appearance,point.X,point.Y,dt,contact,blocked))DeliverDirectCare(CareKind.Pet);
            RenderHoverHand(point,near);
        }
        return DirectCareHolding;
    }
    private void DeliverDirectCare(CareKind kind)
    {
        var contact=directCareSession.CreateContact(appearance,kind);
        physicalCareEvidence=true;
        try
        {
            if(DirectCareContactRequested?.Invoke(contact)!=true)return;
            if(kind==CareKind.Pet)DirectPetContacts++;else DirectGroomContacts++;
            directAffectionUntil=clock.Elapsed.TotalSeconds+1.2;
        }
        finally{physicalCareEvidence=false;}
    }
    internal DirectCareResult ApplyDirectContact(DirectCareContact contact,PetState pet,CompanionState companion)
        =>directCareSession.Apply(contact,appearance,contact.Kind,pet,companion,DateTimeOffset.UtcNow,
            physicalCareEvidence,DirectCareBlocks());
    private void RenderHoverHand(Point point,bool show)
    {
        hoverHand.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(!show)return;
        var local=LocalCarePoint(point);var width=36*sceneScale*116/BodyWidth;
        hoverHand.Width=width;hoverHand.Height=width*handAsset!.Height/handAsset.Width;
        var flip=local.X+width>116;
        hoverHand.RenderTransformOrigin=new(.5,.5);hoverHand.RenderTransform=new ScaleTransform(flip?-1:1,1);
        Canvas.SetLeft(hoverHand,flip?Math.Max(0,local.X-width):local.X);
        Canvas.SetTop(hoverHand,Math.Clamp(local.Y-hoverHand.Height*.4,0,144-hoverHand.Height));
    }
    private void ApplyDirectCareFeedback()
    {
        // Legacy geometry overlays are retired. The hand follows the actual
        // pointer; grooming is represented by the tool the player is holding.
        PettingHand.Visibility=GroomComb.Visibility=Visibility.Collapsed;
        if(clock.Elapsed.TotalSeconds<directAffectionUntil)Affection.Visibility=Visibility.Visible;
    }
}
