namespace Farkit1

open System

open Foundation
open UIKit
open ARKit
open SceneKit

type ARDelegate() =
   inherit ARSCNViewDelegate()

   override this.DidAddNode (renderer, node, anchor) = 
      match anchor <> null && anchor :? ARPlaneAnchor with 
      | true -> anchor :?> ARPlaneAnchor |> this.PlaceAnchorNode node
      | false -> ignore()   

   member this.PlaceAnchorNode ( node : SCNNode) (planeAnchor : ARPlaneAnchor ) = 
     let plane = SCNPlane.Create (nfloat( planeAnchor.Extent.X ) , nfloat ( planeAnchor.Extent.Z ) )
     plane.FirstMaterial.Diffuse.Contents <- UIColor.LightGray
     let planeNode = SCNNode.FromGeometry plane

     //Locate the node at the position of the anchor
     planeNode.Position <- new SCNVector3 (planeAnchor.Extent.X, 0.f ,planeAnchor.Extent.Z)
     // Rotate it to lie flat
     planeNode.Transform <- SCNMatrix4.CreateRotationX <| (float32) (Math.PI / 2.0)
     node.AddChildNode planeNode

     //Mark the anchor with a small red box 
     let box = new SCNBox()
     box.Height <- nfloat 0.05f
     box.Width <- nfloat 0.05
     box.Length <- nfloat 0.05
     box.FirstMaterial.Diffuse.ContentColor <- UIColor.Red

     let anchorNode = new SCNNode()
     anchorNode.Position <- new SCNVector3(0.0f, 0.0f, 0.0f)
     anchorNode.Geometry <- box

     planeNode.AddChildNode anchorNode



[<Register ("ViewController")>]
type ViewController (handle:IntPtr) =
   inherit UIViewController (handle)

   let mutable arsceneview : ARSCNView = null

   let positionFromTransform (xform : OpenTK.Matrix4) = new SceneKit.SCNVector3(xform.M41, xform.M42, xform.M43) 

   (* Load bitmap materials and return in array for applying to a SCNBox *)
   let materials =
      let material fname = 
         let mat = new SCNMaterial()
         mat.Diffuse.Contents <- UIImage.FromFile fname
         mat.LocksAmbientWithDiffuse <- true
         mat

      let a, b, c  = material "msft_logo.png", material "xamagon.png", material "fsharp.png"
      // Put logos on opposite sides of cube
      [| a; b; a; b; c; c |]

   override this.DidReceiveMemoryWarning () =
        base.DidReceiveMemoryWarning ()

   override this.ShouldAutorotateToInterfaceOrientation (toInterfaceOrientation) =
        // Return true for supported orientations
        if UIDevice.CurrentDevice.UserInterfaceIdiom = UIUserInterfaceIdiom.Phone then
           toInterfaceOrientation <> UIInterfaceOrientation.PortraitUpsideDown
        else
           true
              
   override this.ViewDidLoad () =
        base.ViewDidLoad ()
      
        arsceneview <- new ARSCNView()
        arsceneview.Frame <- this.View.Frame
        arsceneview.Delegate <- new ARDelegate()
        arsceneview.DebugOptions <- ARSCNDebugOptions.ShowFeaturePoints + ARSCNDebugOptions.ShowWorldOrigin
        arsceneview.UserInteractionEnabled <- true

        this.View.AddSubview arsceneview

   override this.ViewWillAppear willAnimate = 
      base.ViewWillAppear willAnimate

      // Configure ARKit 
      let configuration = new ARWorldTrackingSessionConfiguration()
      configuration.PlaneDetection <- ARPlaneDetection.Horizontal

      // This method is called subsequent to `ViewDidLoad` so we know arsceneview is instantiated
      arsceneview.Session.Run configuration

   override this.TouchesBegan (touches, evt) = 
      base.TouchesBegan (touches, evt);
      let touch = touches.AnyObject :?> UITouch;
      match touch <> null with 
      | true -> 
         touch.LocationInView(arsceneview)
         |> this.WorldPositionFromHitTest
         |> Option.bind this.PlaceCube
         |> ignore
      | false -> ignore() 

   member this.WorldPositionFromHitTest pt = 
      // Hit-test against existing anchors
      let existingAnchorHits = 
         arsceneview.HitTest ( pt, ARHitTestResultType.ExistingPlaneUsingExtent ) 
         |> Seq.filter (fun result -> result.Anchor :? ARPlaneAnchor)
      match existingAnchorHits |> Seq.isEmpty  with
      | false -> 
         let first = existingAnchorHits |> Seq.head
         let pos = positionFromTransform first.WorldTransform
         match pos.X + pos.Y + pos.Z = 0.f with 
         | true -> None
         | false -> 
            (pos, first.Anchor :?> ARPlaneAnchor) |> Some
      | true -> None  //No plane anchors found

   member this.PlaceCube (worldPosition , planeAnchor )= 
      //Put a box on it
      let box = new SCNBox()
      box.Width <- nfloat(0.1f)
      box.Height <- nfloat(0.1f)
      box.Length <- nfloat(0.1f)

      let cubeNode = new SCNNode()
      cubeNode.Position <- worldPosition
      cubeNode.Geometry <- box

      cubeNode.Geometry.Materials <- materials

      arsceneview.Scene.RootNode.AddChildNode cubeNode
      cubeNode |> Some