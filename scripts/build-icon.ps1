$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root 'src/DesktopLife.App/Assets'
New-Item -ItemType Directory -Force $directory | Out-Null
$bitmap=[Drawing.Bitmap]::new(256,256)
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::Transparent)
$brushes=@{}
foreach($pair in @{background='#213C3B';fur='#E9AB61';cream='#FFF0D2';pink='#CE8E82';ink='#25403D';stripe='#AF6D3D'}.GetEnumerator()){
    $brushes[$pair.Key]=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($pair.Value))
}
$pen=[Drawing.Pen]::new($brushes.ink,5)
$pen.StartCap=[Drawing.Drawing2D.LineCap]::Round
$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
$stripe=[Drawing.Pen]::new($brushes.stripe,8)
$stripe.StartCap=[Drawing.Drawing2D.LineCap]::Round
$stripe.EndCap=[Drawing.Drawing2D.LineCap]::Round
try{
    $graphics.FillEllipse($brushes.background,4,4,248,248)
    $graphics.FillPolygon($brushes.fur,[Drawing.Point[]]@([Drawing.Point]::new(49,123),[Drawing.Point]::new(50,39),[Drawing.Point]::new(107,80)))
    $graphics.FillPolygon($brushes.fur,[Drawing.Point[]]@([Drawing.Point]::new(149,80),[Drawing.Point]::new(206,39),[Drawing.Point]::new(207,123)))
    $graphics.FillPolygon($brushes.pink,[Drawing.Point[]]@([Drawing.Point]::new(61,97),[Drawing.Point]::new(60,55),[Drawing.Point]::new(92,79)))
    $graphics.FillPolygon($brushes.pink,[Drawing.Point[]]@([Drawing.Point]::new(164,79),[Drawing.Point]::new(196,55),[Drawing.Point]::new(195,97)))
    $graphics.FillEllipse($brushes.fur,39,66,178,159)
    $graphics.FillEllipse($brushes.cream,70,140,116,77)
    $graphics.FillEllipse($brushes.cream,69,105,38,51)
    $graphics.FillEllipse($brushes.cream,149,105,38,51)
    $graphics.FillEllipse($brushes.ink,82,111,16,39)
    $graphics.FillEllipse($brushes.ink,158,111,16,39)
    $graphics.FillEllipse([Drawing.Brushes]::White,88,115,7,11)
    $graphics.FillEllipse([Drawing.Brushes]::White,165,115,7,11)
    $graphics.DrawLine($stripe,112,80,118,98)
    $graphics.DrawLine($stripe,128,76,128,95)
    $graphics.DrawLine($stripe,144,80,138,98)
    $graphics.FillPolygon($brushes.pink,[Drawing.Point[]]@([Drawing.Point]::new(119,157),[Drawing.Point]::new(137,157),[Drawing.Point]::new(128,168)))
    $graphics.DrawArc($pen,110,162,18,17,0,150)
    $graphics.DrawArc($pen,128,162,18,17,30,150)
    $graphics.DrawLine($pen,78,163,38,155)
    $graphics.DrawLine($pen,76,175,35,178)
    $graphics.DrawLine($pen,178,163,218,155)
    $graphics.DrawLine($pen,180,175,221,178)
    $png=[IO.MemoryStream]::new()
    $bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
    $file=[IO.File]::Create((Join-Path $directory 'DesktopLife.ico'))
    $writer=[IO.BinaryWriter]::new($file)
    try{
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1)
        $writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
        $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$png.Length);$writer.Write([uint32]22)
        $writer.Write($png.ToArray())
    }finally{$writer.Dispose();$png.Dispose()}
}finally{
    $graphics.Dispose();$bitmap.Dispose();$pen.Dispose();$stripe.Dispose()
    foreach($brush in $brushes.Values){$brush.Dispose()}
}
