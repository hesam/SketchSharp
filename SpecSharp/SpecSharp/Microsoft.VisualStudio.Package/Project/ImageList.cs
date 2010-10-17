//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.IO;
using System.Windows.Forms;

namespace Microsoft.VisualStudio.Package{
	/// <summary>
	/// Summary description for ImageList.
	/// </summary>
  public class ImageList : IDisposable {
    public Images Images;
    ArrayList images;
    public Color transparentColor;   
    System.Windows.Forms.ImageList iList;
    Size imageSize;

    public ImageList() {		
	    images = new ArrayList();      
      this.Images = new Images(this.images);
    }
    public void Dispose() {
      if (iList != null) {
        iList.Dispose();
        iList = null;
      }
      if (images != null) {
//        foreach (Bitmap m in images) {  //hv: Cloning the ImageList does not copy the bit maps, so they can still be alive at this point
//          m.Dispose();
//        }
        images.Clear();
      } 
      images = null;
    }

    public ImageList Clone() {
      ImageList n = new ImageList();
      n.images = (ArrayList)this.images.Clone();
      n.Images = new Images(n.images);
      n.transparentColor = this.transparentColor;
      n.imageSize = this.imageSize;
      return n;
    }

    public void AddImages(string resourceName, Assembly assembly, 
      int numberOfImages, int width, int height, Color transparentColor) {
      this.imageSize = new Size(width, height);
      this.transparentColor = transparentColor;
      Stream s = assembly.GetManifestResourceStream(resourceName);
      if (s != null) {
        Image img = Bitmap.FromStream(s);
        s.Close();
        for (int i = 0; i < numberOfImages; i++) {
          images.Add(ExtractImage(img, width*i, 0, width, height, transparentColor));
        }
      }
    }

    public uint GetColorRef(Color c) {
      return (uint)c.R | ((uint)c.G << 8) | ((uint)c.B << 16);
    }


    public IntPtr GetNativeImageList() {     
      if (iList == null) {
        iList = new System.Windows.Forms.ImageList();
        iList.ColorDepth = ColorDepth.Depth4Bit;
        iList.TransparentColor = this.transparentColor;
        iList.ImageSize = this.imageSize;
            
        for (int i = 0; i < this.images.Count; i++) {
          Bitmap bmp = this.GetBitmap(i);
          iList.Images.Add(bmp, this.transparentColor);        
        }
      }
      return iList.Handle;
    }

    public Bitmap GetBitmap(int index) {
      return (Bitmap)this.images[index];
    }

    Bitmap ExtractImage(Image img, int x, int y, int w, int h, Color transparentColor) {
      Bitmap result = new Bitmap(w,h);
      Graphics g = Graphics.FromImage(result);
      g.DrawImage(img, 0, 0, new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
      if (transparentColor != Color.Transparent) {
        result.MakeTransparent(transparentColor);
      }
      g.Dispose();
      return result;
    }

  }
  public class Images{
    internal ArrayList images;
    public Images(ArrayList images){
      this.images = images;
    }
    public Bitmap this [int index] {
      get {
        if (index <= this.images.Count)
          return (Bitmap)images[index];
        
        return null;
      }
    }    
  }
}
