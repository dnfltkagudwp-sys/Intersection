# 브라우저·지도 레코드 미리보기 PNG 생성 (Windows PowerShell 5.1, System.Drawing).
# 출력: Assets/Art/BrowserPreviews/<레코드명>.png (1080x1350), Assets/Art/MapPreviews/<레코드명>.png (1080x1080)
# 이미지 속 글자는 쓰지 않는다. 글줄은 회색 막대로만 표시해 언어와 무관하게 둔다.
# 사용: powershell -ExecutionPolicy Bypass -File Tools/make_record_previews.ps1 [-Only 레코드명,...]
# -Only를 주면 그 이름의 PNG만 다시 저장한다(나머지는 그리기만 하고 저장하지 않음).
param([string]$Root = (Split-Path -Parent $PSScriptRoot), [string[]]$Only = @())
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })  # -File 실행 시 쉼표 목록이 한 문자열로 들어온다

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$BrowserDir = Join-Path $Root 'Assets/Art/BrowserPreviews'
$MapDir = Join-Path $Root 'Assets/Art/MapPreviews'
New-Item -ItemType Directory -Force $BrowserDir, $MapDir | Out-Null

function C([string]$hex, [int]$a = 255) {
    $h = $hex.TrimStart('#')
    [Drawing.Color]::FromArgb($a, [Convert]::ToInt32($h.Substring(0, 2), 16), [Convert]::ToInt32($h.Substring(2, 2), 16), [Convert]::ToInt32($h.Substring(4, 2), 16))
}

# 공통 색
$P = @{
    bg = C 'FFFFFF'; chrome = C 'F1F2F4'; line = C 'E2E4E8'; text = C '3A3D44'; sub = C 'B9BDC6'; faint = C 'D9DCE2'
    link = C '4C5E9E'; accent = C '5C6F8F'; box = C 'F3F5F8'; thumb = C 'E7E9EE'
}

function New-Canvas([int]$w, [int]$h, $bg) {
    $b = New-Object Drawing.Bitmap $w, $h
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear($bg)
    @{ bmp = $b; g = $g }
}

function RRPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    if ($d -le 0) { $p.AddRectangle((New-Object Drawing.RectangleF $x, $y, $w, $h)); return $p }
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function Fill-RR($g, $col, $x, $y, $w, $h, $r) {
    $p = RRPath $x $y $w $h $r; $br = New-Object Drawing.SolidBrush $col
    $g.FillPath($br, $p); $br.Dispose(); $p.Dispose()
}
function Stroke-RR($g, $col, [float]$wd, $x, $y, $w, $h, $r) {
    $p = RRPath $x $y $w $h $r; $pen = New-Object Drawing.Pen $col, $wd
    $g.DrawPath($pen, $p); $pen.Dispose(); $p.Dispose()
}
function Fill-Circle($g, $col, $cx, $cy, $r) {
    $br = New-Object Drawing.SolidBrush $col; $g.FillEllipse($br, [float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r)); $br.Dispose()
}
function Stroke-Circle($g, $col, [float]$wd, $cx, $cy, $r) {
    $pen = New-Object Drawing.Pen $col, $wd; $g.DrawEllipse($pen, [float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r)); $pen.Dispose()
}
function Line($g, $col, [float]$wd, $x1, $y1, $x2, $y2) {
    $pen = New-Object Drawing.Pen $col, $wd; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLine($pen, [float]$x1, [float]$y1, [float]$x2, [float]$y2); $pen.Dispose()
}
# 글줄 대신 쓰는 둥근 막대
function Bar($g, $col, $x, $y, $w, $h = 18) { Fill-RR $g $col $x $y $w $h ($h / 2) }

# ---------- 작은 그림 기호(글자 없음) ----------
function Icon($g, [string]$kind, $cx, $cy, $s, $col) {
    $pen = New-Object Drawing.Pen $col, ([float][Math]::Max(3, $s * 0.09)); $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $br = New-Object Drawing.SolidBrush $col
    $h = $s / 2
    switch ($kind) {
        'shirt' {
            $pts = @(
                (New-Object Drawing.PointF ($cx - $h * 0.35), ($cy - $h * 0.8)), (New-Object Drawing.PointF ($cx - $h), ($cy - $h * 0.45)),
                (New-Object Drawing.PointF ($cx - $h * 0.75), ($cy - $h * 0.05)), (New-Object Drawing.PointF ($cx - $h * 0.55), ($cy - $h * 0.2)),
                (New-Object Drawing.PointF ($cx - $h * 0.55), ($cy + $h * 0.85)), (New-Object Drawing.PointF ($cx + $h * 0.55), ($cy + $h * 0.85)),
                (New-Object Drawing.PointF ($cx + $h * 0.55), ($cy - $h * 0.2)), (New-Object Drawing.PointF ($cx + $h * 0.75), ($cy - $h * 0.05)),
                (New-Object Drawing.PointF ($cx + $h), ($cy - $h * 0.45)), (New-Object Drawing.PointF ($cx + $h * 0.35), ($cy - $h * 0.8)))
            $g.FillPolygon($br, [Drawing.PointF[]]$pts)
        }
        'cup' {
            $g.FillRectangle($br, [float]($cx - $h * 0.6), [float]($cy - $h * 0.35), [float]($h * 1.0), [float]($h * 0.95))
            $g.DrawArc($pen, [float]($cx + $h * 0.2), [float]($cy - $h * 0.2), [float]($h * 0.55), [float]($h * 0.5), -90, 180)
            $g.DrawLine($pen, [float]($cx - $h * 0.8), [float]($cy + $h * 0.75), [float]($cx + $h * 0.6), [float]($cy + $h * 0.75))
        }
        'calendar' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.8), [float]($cy - $h * 0.6), [float]($h * 1.6), [float]($h * 1.4))
            $g.FillRectangle($br, [float]($cx - $h * 0.8), [float]($cy - $h * 0.6), [float]($h * 1.6), [float]($h * 0.35))
            for ($i = 0; $i -lt 3; $i++) { for ($j = 0; $j -lt 2; $j++) { $g.FillRectangle($br, [float]($cx - $h * 0.55 + $i * $h * 0.45), [float]($cy - $h * 0.05 + $j * $h * 0.4), [float]($h * 0.22), [float]($h * 0.22)) } }
        }
        'doc' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.6), [float]($cy - $h * 0.8), [float]($h * 1.2), [float]($h * 1.6))
            for ($i = 0; $i -lt 4; $i++) { $g.DrawLine($pen, [float]($cx - $h * 0.35), [float]($cy - $h * 0.4 + $i * $h * 0.33), [float]($cx + $h * 0.35), [float]($cy - $h * 0.4 + $i * $h * 0.33)) }
        }
        'shield' {
            $pts = @((New-Object Drawing.PointF $cx, ($cy - $h * 0.85)), (New-Object Drawing.PointF ($cx + $h * 0.75), ($cy - $h * 0.55)),
                (New-Object Drawing.PointF ($cx + $h * 0.65), ($cy + $h * 0.3)), (New-Object Drawing.PointF $cx, ($cy + $h * 0.85)),
                (New-Object Drawing.PointF ($cx - $h * 0.65), ($cy + $h * 0.3)), (New-Object Drawing.PointF ($cx - $h * 0.75), ($cy - $h * 0.55)))
            $g.DrawPolygon($pen, [Drawing.PointF[]]$pts)
        }
        'bubble' {
            $g.DrawEllipse($pen, [float]($cx - $h * 0.85), [float]($cy - $h * 0.7), [float]($h * 1.7), [float]($h * 1.2))
            $g.DrawLine($pen, [float]($cx - $h * 0.35), [float]($cy + $h * 0.42), [float]($cx - $h * 0.6), [float]($cy + $h * 0.85))
        }
        'camera' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.85), [float]($cy - $h * 0.45), [float]($h * 1.7), [float]($h * 1.1))
            $g.DrawRectangle($pen, [float]($cx - $h * 0.35), [float]($cy - $h * 0.7), [float]($h * 0.7), [float]($h * 0.25))
            $g.DrawEllipse($pen, [float]($cx - $h * 0.35), [float]($cy - $h * 0.25), [float]($h * 0.7), [float]($h * 0.7))
        }
        'check' {
            for ($i = 0; $i -lt 3; $i++) {
                $yy = $cy - $h * 0.6 + $i * $h * 0.6
                $g.DrawRectangle($pen, [float]($cx - $h * 0.8), [float]($yy - $h * 0.18), [float]($h * 0.36), [float]($h * 0.36))
                $g.DrawLine($pen, [float]($cx - $h * 0.2), [float]$yy, [float]($cx + $h * 0.8), [float]$yy)
            }
        }
        'lock' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.6), [float]($cy - $h * 0.1), [float]($h * 1.2), [float]($h * 0.9))
            $g.DrawArc($pen, [float]($cx - $h * 0.4), [float]($cy - $h * 0.75), [float]($h * 0.8), [float]($h * 1.1), 180, 180)
        }
        'download' {
            $g.DrawLine($pen, [float]$cx, [float]($cy - $h * 0.8), [float]$cx, [float]($cy + $h * 0.3))
            $g.DrawLine($pen, [float]($cx - $h * 0.4), [float]($cy - $h * 0.1), [float]$cx, [float]($cy + $h * 0.3))
            $g.DrawLine($pen, [float]($cx + $h * 0.4), [float]($cy - $h * 0.1), [float]$cx, [float]($cy + $h * 0.3))
            $g.DrawLine($pen, [float]($cx - $h * 0.8), [float]($cy + $h * 0.75), [float]($cx + $h * 0.8), [float]($cy + $h * 0.75))
        }
        'pin' {
            $g.FillEllipse($br, [float]($cx - $h * 0.5), [float]($cy - $h * 0.85), [float]$h, [float]$h)
            $pts = @((New-Object Drawing.PointF ($cx - $h * 0.42), ($cy - $h * 0.2)), (New-Object Drawing.PointF ($cx + $h * 0.42), ($cy - $h * 0.2)), (New-Object Drawing.PointF $cx, ($cy + $h * 0.85)))
            $g.FillPolygon($br, [Drawing.PointF[]]$pts)
        }
        'people' {
            foreach ($k in -1, 0, 1) {
                $px = $cx + $k * $h * 0.62; $sz = if ($k -eq 0) { 1.0 } else { 0.85 }
                $g.FillEllipse($br, [float]($px - $h * 0.2 * $sz), [float]($cy - $h * 0.62 * $sz), [float]($h * 0.4 * $sz), [float]($h * 0.4 * $sz))
                $g.FillPie($br, [float]($px - $h * 0.36 * $sz), [float]($cy - $h * 0.12), [float]($h * 0.72 * $sz), [float]($h * 1.2 * $sz), 180, 180)
            }
        }
        'globe' {
            $g.DrawEllipse($pen, [float]($cx - $h * 0.8), [float]($cy - $h * 0.8), [float]($h * 1.6), [float]($h * 1.6))
            $g.DrawEllipse($pen, [float]($cx - $h * 0.35), [float]($cy - $h * 0.8), [float]($h * 0.7), [float]($h * 1.6))
            $g.DrawLine($pen, [float]($cx - $h * 0.8), [float]$cy, [float]($cx + $h * 0.8), [float]$cy)
        }
        'box' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.75), [float]($cy - $h * 0.55), [float]($h * 1.5), [float]($h * 1.25))
            $g.DrawLine($pen, [float]($cx - $h * 0.75), [float]($cy - $h * 0.15), [float]($cx + $h * 0.75), [float]($cy - $h * 0.15))
            $g.DrawLine($pen, [float]$cx, [float]($cy - $h * 0.55), [float]$cx, [float]($cy - $h * 0.15))
        }
        'building' {
            $g.DrawRectangle($pen, [float]($cx - $h * 0.7), [float]($cy - $h * 0.75), [float]($h * 1.4), [float]($h * 1.55))
            for ($i = 0; $i -lt 3; $i++) { for ($j = 0; $j -lt 3; $j++) { $g.FillRectangle($br, [float]($cx - $h * 0.45 + $i * $h * 0.35), [float]($cy - $h * 0.5 + $j * $h * 0.4), [float]($h * 0.18), [float]($h * 0.2)) } }
        }
    }
    $pen.Dispose(); $br.Dispose()
}

# 계정마다 정해지는 낮은 채도 무늬의 추상 기본 프로필(얼굴 없음)
function Avatar($g, $cx, $cy, $r, [int]$seed) {
    $rnd = New-Object Random $seed
    $base = @('C9D3D6', 'D8CFC4', 'CCD2C2', 'D4CAD3', 'C8CCD8')[$seed % 5]
    Fill-Circle $g (C $base) $cx $cy $r
    $clip = New-Object Drawing.Drawing2D.GraphicsPath; $clip.AddEllipse([float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r))
    $g.SetClip($clip)
    for ($i = 0; $i -lt 4; $i++) {
        $col = C (@('AEB9BD', 'BFB3A6', 'B3BAA6', 'BCAFBB', 'AEB3C2')[($seed + $i) % 5]) 170
        Fill-Circle $g $col ($cx + $rnd.Next(-[int]$r, [int]$r)) ($cy + $rnd.Next(-[int]$r, [int]$r)) ($r * (0.45 + $rnd.NextDouble() * 0.4))
    }
    $g.ResetClip(); $clip.Dispose()
}

# 캡처 렌더러(CaptureRenderer.AccountAvatar)와 같은 규칙: 계정명 FNV-1a 해시로 팔레트 색·도형을 고른다.
# 팔레트는 CaptureStyle_SnsApp.avatarPalette 값. 같은 계정명이면 캡처와 같은 프로필 무늬가 된다.
$AvatarPalette = @(@(0.7882353, 0.827451, 0.8392157), @(0.84705883, 0.8117647, 0.76862746), @(0.78039217, 0.80784315, 0.7529412), @(0.8352941, 0.7882353, 0.8117647),
    @(0.7254902, 0.7607843, 0.7882353), @(0.8117647, 0.77254903, 0.70980394), @(0.7490196, 0.78431374, 0.7372549), @(0.7764706, 0.74509805, 0.78431374))
function AccountHash([string]$account) {
    [uint64]$h = 2166136261
    foreach ($ch in $account.ToCharArray()) { $h = $h -bxor [uint64][int]$ch; $h = ($h * 16777619) % 4294967296 }
    $h
}
function AccountAvatar($g, $cx, $cy, $r, [string]$account) {
    $h = AccountHash $account; $n = [uint64]$AvatarPalette.Count
    $bi = [int]($h % $n)
    $fi = [int](([uint64][Math]::Floor($h / $n) + 1 + $h % $n) % $n)
    if ($fi -eq $bi) { $fi = ($bi + 1) % $n }
    $bg = $AvatarPalette[$bi]; $fg = $AvatarPalette[$fi]
    $ac = @(0..2 | ForEach-Object { $fg[$_] + (1 - $fg[$_]) * 0.35 })
    $shape = [int]([uint64][Math]::Floor($h / 65536) % 3)
    $size = [int]($r * 2); $bmp = New-Object Drawing.Bitmap $size, $size
    $rad = $size * 0.5
    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $u = ($x + 0.5) / $size; $v = ($y + 0.5) / $size
            $c = $bg
            switch ($shape) {
                0 { if ([Math]::Sqrt(($u - 0.5) * ($u - 0.5) + ($v - 1.15) * ($v - 1.15)) -lt 0.62) { $c = $fg } }
                1 { $k = $u + $v; if ($k -gt 0.85 -and $k -lt 1.25) { $c = $fg } elseif ($k -ge 1.25) { $c = $ac } }
                default { if ([Math]::Sqrt(($u - 0.62) * ($u - 0.62) + ($v - 0.4) * ($v - 0.4)) -lt 0.24) { $c = $fg } elseif ($v -gt 0.72) { $c = $ac } }
            }
            $d = [Math]::Sqrt(($x + 0.5 - $rad) * ($x + 0.5 - $rad) + ($y + 0.5 - $rad) * ($y + 0.5 - $rad))
            $a = [Math]::Max(0, [Math]::Min(1, $rad - $d + 0.5))
            $bmp.SetPixel($x, $y, [Drawing.Color]::FromArgb([int]($a * 255), [int][Math]::Round($c[0] * 255), [int][Math]::Round($c[1] * 255), [int][Math]::Round($c[2] * 255)))
        }
    }
    $g.DrawImage($bmp, [float]($cx - $r), [float]($cy - $r), [float]$size, [float]$size); $bmp.Dispose()
}
# 썸네일: 둥근 바탕 + 기호
function Thumb($g, $x, $y, $w, $h, [string]$bgHex, [string]$icon, [string]$iconHex = '8A93A6') {
    Fill-RR $g (C $bgHex) $x $y $w $h 14
    if ($icon) { Icon $g $icon ($x + $w / 2) ($y + $h / 2) ([Math]::Min($w, $h) * 0.5) (C $iconHex) }
}

# ---------- 브라우저 틀 ----------
function Browser-Chrome($g) {
    $g.FillRectangle((New-Object Drawing.SolidBrush $P.chrome), 0, 0, 1080, 130)
    Fill-RR $g $P.bg 36 32 1008 66 33
    Fill-Circle $g $P.faint 82 65 13
    Bar $g $P.faint 112 56 360 18
    Line $g $P.line 2 0 130 1080 130
}

function Search-Header($g, [int]$queryLen) {
    Stroke-RR $g $P.faint 3 48 160 984 92 46
    Stroke-Circle $g $P.accent 5 106 200 17
    Line $g $P.accent 5 118 212 132 226
    Bar $g $P.text 150 196 ([Math]::Min(720, 40 + $queryLen * 34)) 20
    # 탭 줄
    $xs = 60
    foreach ($tw in 70, 90, 80, 70) { Bar $g $P.sub $xs 288 $tw 16; $xs += $tw + 48 }
    Line $g $P.accent 5 60 322 130 322
    Line $g $P.line 2 0 326 1080 326
}

# 일반 검색 결과 한 건. $thumb = @{bg; icon} 또는 $null
function Result($g, $y, $seed, $thumb = $null, [int]$lines = 2, [string]$fav = 'globe') {
    $rnd = New-Object Random $seed
    Fill-Circle $g $P.box 78 ($y + 30) 20
    Icon $g $fav 78 ($y + 30) 26 $P.sub
    Bar $g $P.faint 112 ($y + 22) (160 + $rnd.Next(0, 120)) 16
    $tw = if ($thumb) { 520 } else { 760 }
    Bar $g $P.link 60 ($y + 72) ($tw - $rnd.Next(0, 200)) 26
    for ($i = 0; $i -lt $lines; $i++) { Bar $g $P.sub 60 ($y + 122 + $i * 34) ($tw + 60 - $rnd.Next(0, 260)) 18 }
    if ($thumb) { Thumb $g 790 ($y + 50) 230 170 $thumb.bg $thumb.icon }
}

function Save-Png($c, [string]$path) {
    $c.g.Dispose()
    if ($Only.Count -gt 0 -and $Only -notcontains [IO.Path]::GetFileNameWithoutExtension($path)) { $c.bmp.Dispose(); return }
    $c.bmp.Save($path, [Drawing.Imaging.ImageFormat]::Png); $c.bmp.Dispose()
    "saved $([IO.Path]::GetFileName($path))"
}

# 검색 결과 화면. $spec: q(검색어 길이), items = 결과 배열(각 @{t='r'|'feat'|'grid'|'strip'|'profile'|'map'; ...})
function Search-Page([string]$name, [int]$qlen, $items) {
    $c = New-Canvas 1080 1350 $P.bg; $g = $c.g
    Browser-Chrome $g; Search-Header $g $qlen
    $y = 360
    foreach ($it in $items) {
        switch ($it.t) {
            'r' { Result $g $y $it.seed $it.thumb $it.lines $it.fav; $y += 250 }
            'feat' {
                Fill-RR $g $P.box 40 $y 1000 300 22
                Fill-Circle $g (C 'FFFFFF') 120 ($y + 90) 50
                Icon $g $it.icon 120 ($y + 90) 56 $P.accent
                Bar $g $P.text 200 ($y + 60) 420 24
                Bar $g $P.sub 200 ($y + 104) 560 18
                for ($i = 0; $i -lt 3; $i++) { Bar $g $P.sub 80 ($y + 170 + $i * 36) (860 - $i * 140) 18 }
                $y += 340
            }
            'strip' {
                Result $g $y $it.seed $null 1 $it.fav
                for ($i = 0; $i -lt 3; $i++) { Thumb $g (60 + $i * 330) ($y + 180) 300 240 $it.bgs[$i] $it.icon $it.iconHex }
                $y += 460
            }
            'grid' {
                for ($r = 0; $r -lt 2; $r++) { for ($k = 0; $k -lt 3; $k++) { Thumb $g (48 + $k * 334) ($y + $r * 262) 316 246 $it.bgs[($r * 3 + $k) % $it.bgs.Count] $it.icon $it.iconHex } }
                $y += 560
            }
            'profile' {
                if ($it.account) { AccountAvatar $g 120 ($y + 80) 64 $it.account } else { Avatar $g 120 ($y + 80) 64 $it.seed }
                Bar $g $P.text 210 ($y + 46) 260 26
                Bar $g $P.sub 210 ($y + 94) 380 18
                Bar $g $P.sub 210 ($y + 128) 300 18
                if ($it.tiles) { for ($i = 0; $i -lt 3; $i++) { Thumb $g (60 + $i * 330) ($y + 190) 300 300 $it.tiles[$i] $it.icon $it.iconHex }; $y += 530 } else { $y += 210 }
            }
            'map' {
                Fill-RR $g (C 'EEF0EA') 40 $y 1000 380 22
                $g.SetClip((RRPath 40 $y 1000 380 22))
                foreach ($k in 0..5) { Line $g (C 'FFFFFF') 18 (40 + $k * 190) $y (100 + $k * 190) ($y + 380) }
                Line $g (C 'FFFFFF') 30 40 ($y + 230) 1040 ($y + 200)
                Fill-RR $g (C 'D9E3D2') 120 ($y + 40) 260 140 10
                $g.ResetClip()
                Icon $g 'pin' 620 ($y + 150) 70 (C 'C0574B')
                Icon $g 'pin' 780 ($y + 120) 50 (C '9AA1AE')
                Icon $g 'pin' 470 ($y + 300) 50 (C '9AA1AE')
                $y += 410
                Result $g $y $it.seed @{ bg = 'EDE6DD'; icon = 'cup' } 1 'pin'
                $y += 250
            }
        }
    }
    # 화면 아래가 비지 않도록 일반 결과로 채운다(한 화면 분량까지만)
    $k = 0
    while ($y -lt 1110) { Result $g $y ($qlen * 97 + $k) $null 2 'globe'; $y += 250; $k++ }
    Save-Png $c (Join-Path $BrowserDir "$name.png")
}

# ---------- 사건 브라우저 ----------
Search-Page 'A_A02-S1' 13 @(
    @{ t = 'strip'; seed = 11; fav = 'shirt'; icon = 'shirt'; iconHex = 'A59C90'; bgs = @('E9E4DC', 'E1E6EA', 'E6E1E8') },
    @{ t = 'r'; seed = 12; fav = 'shirt'; lines = 2; thumb = @{ bg = 'E7E3DA'; icon = 'shirt' } },
    @{ t = 'r'; seed = 13; fav = 'bubble'; lines = 2 })
Search-Page 'A_A02-S2' 10 @(
    @{ t = 'profile'; seed = 21 },
    @{ t = 'r'; seed = 22; fav = 'building'; lines = 2; thumb = @{ bg = 'E2E7EE'; icon = 'calendar' } },
    @{ t = 'r'; seed = 23; fav = 'building'; lines = 2; thumb = @{ bg = 'E6E9E2'; icon = 'people' } },
    @{ t = 'r'; seed = 24; fav = 'globe'; lines = 2 })
Search-Page 'A_A02-S3' 8 @(
    @{ t = 'map'; seed = 31 },
    @{ t = 'r'; seed = 32; fav = 'pin'; lines = 2; thumb = @{ bg = 'EAE3DA'; icon = 'cup' } })
Search-Page 'A_A02-S4' 12 @(
    @{ t = 'profile'; seed = 41 },
    @{ t = 'profile'; seed = 42 },
    @{ t = 'profile'; seed = 43 },
    @{ t = 'r'; seed = 44; fav = 'globe'; lines = 2 })
Search-Page 'A_A02-S5' 8 @(
    @{ t = 'r'; seed = 51; fav = 'building'; lines = 1 },
    @{ t = 'grid'; icon = 'people'; iconHex = 'A6ADB8'; bgs = @('E4E8EC', 'E9E5DE', 'E2E7E1', 'E7E2E8', 'E3E6EC', 'EAE6DF') })
Search-Page 'A_A02-S6' 14 @(
    @{ t = 'feat'; icon = 'download' },
    @{ t = 'r'; seed = 61; fav = 'doc'; lines = 2; thumb = @{ bg = 'E4E7EC'; icon = 'download' } },
    @{ t = 'r'; seed = 62; fav = 'doc'; lines = 2; thumb = @{ bg = 'E7E4EC'; icon = 'camera' } })
Search-Page 'A_D01-S1' 3 @(
    @{ t = 'profile'; seed = 71; icon = 'shirt'; iconHex = 'A59C90'; tiles = @('E8E2DA', 'E0E5E9', 'E5E2DB') },
    @{ t = 'r'; seed = 72; fav = 'bubble'; lines = 2 })
Search-Page 'A_D01-S2' 7 @(
    @{ t = 'profile'; seed = 71 },
    @{ t = 'r'; seed = 73; fav = 'shirt'; lines = 2; thumb = @{ bg = 'E8E2DA'; icon = 'shirt' } },
    @{ t = 'r'; seed = 74; fav = 'bubble'; lines = 2 },
    @{ t = 'r'; seed = 75; fav = 'globe'; lines = 2 })

# B02: 같은 검색 화면 디자인. 위 요약 상자 + 결과 2건, 기호만 검색어 흐름에 맞춰 바뀐다.
$b02 = @(
    @{ n = 'B_B02-S1'; q = 9; icon = 'doc'; r = @('shield', 'doc') },
    @{ n = 'B_B02-S2'; q = 11; icon = 'bubble'; r = @('bubble', 'shield') },
    @{ n = 'B_B02-S3'; q = 14; icon = 'camera'; r = @('shield', 'bubble') },
    @{ n = 'B_B02-S4'; q = 10; icon = 'check'; r = @('doc', 'shield') },
    @{ n = 'B_B02-S5'; q = 17; icon = 'lock'; r = @('bubble', 'shield') },
    @{ n = 'B_B02-S6'; q = 11; icon = 'calendar'; r = @('building', 'calendar') })
$i = 0
foreach ($b in $b02) {
    $i++
    Search-Page $b.n $b.q @(
        @{ t = 'feat'; icon = $b.icon },
        @{ t = 'r'; seed = 100 + $i * 2; fav = $b.r[0]; lines = 2 },
        @{ t = 'r'; seed = 101 + $i * 2; fav = $b.r[1]; lines = 2 })
}

# B09-2: 계정을 찾은 검색 결과
Search-Page 'B_P3-B09-2' 10 @(
    @{ t = 'profile'; seed = 91; account = 'daylog_041'; tiles = @('E3E5E8', 'E6E3DE', 'E1E4E0') },
    @{ t = 'r'; seed = 92; fav = 'globe'; lines = 2 })

# B09-1: 실제로 열린 프로필 페이지(방문 기록). 추상 프로필·숫자 막대·버튼·3칸 그리드
function Profile-Page([string]$name, [string]$account) {
    $c = New-Canvas 1080 1350 $P.bg; $g = $c.g
    Browser-Chrome $g
    Bar $g $P.text 60 170 260 28
    AccountAvatar $g 160 340 110 $account
    foreach ($k in 0..2) { Bar $g $P.text (360 + $k * 230) 300 70 30; Bar $g $P.sub (345 + $k * 230) 350 100 16 }
    Bar $g $P.sub 60 490 520 18
    Bar $g $P.sub 60 524 380 18
    Fill-RR $g $P.link 60 580 470 70 16
    Fill-RR $g $P.box 550 580 470 70 16
    Line $g $P.line 2 0 690 1080 690
    foreach ($k in 0..2) { Fill-RR $g $P.faint (150 + $k * 330) 712 60 36 6 }
    Line $g $P.text 4 60 770 380 770
    $bgs = 'DCE0E4', 'E3DED6', 'DEE2DA'
    foreach ($k in 0..2) { Thumb $g (4 + $k * 358) 776 354 354 $bgs[$k] $null }
    Save-Png $c (Join-Path $BrowserDir "$name.png")
}
Profile-Page 'B_P3-B09-1' 'daylog_041'

# ---------- 생활 브라우저 공용 틀 ----------
Search-Page 'Common_Search' 9 @(
    @{ t = 'r'; seed = 201; fav = 'globe'; lines = 2; thumb = @{ bg = 'E5E8EC'; icon = 'doc' } },
    @{ t = 'r'; seed = 202; fav = 'building'; lines = 2 },
    @{ t = 'r'; seed = 203; fav = 'bubble'; lines = 2 },
    @{ t = 'r'; seed = 204; fav = 'globe'; lines = 1 })

function Notice-Page {
    $c = New-Canvas 1080 1350 $P.bg; $g = $c.g
    Browser-Chrome $g
    $g.FillRectangle((New-Object Drawing.SolidBrush (C '5C6F8F')), 0, 131, 1080, 150)
    Fill-Circle $g (C 'FFFFFF' 230) 110 206 42
    Icon $g 'building' 110 206 50 (C '5C6F8F')
    Bar $g (C 'FFFFFF' 230) 175 186 300 26
    Bar $g (C 'FFFFFF' 150) 175 228 200 16
    Bar $g $P.text 60 330 520 32
    Bar $g $P.sub 60 384 260 16
    Line $g $P.line 2 60 430 1020 430
    for ($r = 0; $r -lt 6; $r++) {
        $yy = 460 + $r * 96
        Fill-RR $g $P.box 60 $yy 150 50 10
        Bar $g $P.sub 230 ($yy + 16) (600 - ($r % 3) * 120) 18
        Line $g $P.line 2 60 ($yy + 78) 1020 ($yy + 78)
    }
    Fill-RR $g $P.box 60 1060 960 200 18
    Icon $g 'calendar' 150 1160 70 $P.accent
    Bar $g $P.sub 230 1120 560 18; Bar $g $P.sub 230 1160 480 18; Bar $g $P.sub 230 1200 380 18
    Save-Png $c (Join-Path $BrowserDir 'Common_Notice.png')
}
Notice-Page

function Lookup-Page {
    $c = New-Canvas 1080 1350 $P.bg; $g = $c.g
    Browser-Chrome $g
    Bar $g $P.text 60 180 340 32
    Stroke-RR $g $P.faint 3 60 250 700 90 16
    Bar $g $P.text 92 286 300 20
    Fill-RR $g $P.link 790 250 230 90 16
    Line $g $P.line 2 0 390 1080 390
    Fill-Circle $g $P.box 140 500 70
    Icon $g 'box' 140 500 80 $P.accent
    Bar $g $P.text 250 466 360 26; Bar $g $P.sub 250 512 260 18
    # 진행 단계 점
    $done = 3
    for ($k = 0; $k -lt 5; $k++) {
        $x = 120 + $k * 210
        if ($k -lt 4) { Line $g ($(if ($k -lt $done - 1) { $P.accent } else { $P.faint })) 6 $x 650 ($x + 210) 650 }
        Fill-Circle $g ($(if ($k -lt $done) { $P.accent } else { $P.faint })) $x 650 20
        Bar $g $P.sub ($x - 45) 694 90 16
    }
    for ($r = 0; $r -lt 4; $r++) {
        $yy = 800 + $r * 110
        Bar $g $P.sub 60 $yy 180 18
        Bar $g $P.text 300 $yy (420 - $r * 60) 18
        Line $g $P.line 2 60 ($yy + 60) 1020 ($yy + 60)
    }
    Save-Png $c (Join-Path $BrowserDir 'Common_Lookup.png')
}
Lookup-Page

# ---------- 지도 ----------
$M = @{ ground = C 'EFEEE9'; block = C 'E1DFD8'; road = C 'FFFFFF'; big = C 'F6E7B8'; bigEdge = C 'E8D394'; green = C 'D5E3CC'; bld = C 'C9D6C0'; water = C 'D4E1EA' }

function Road($g, $col, [float]$wd, $pts) {
    $pen = New-Object Drawing.Pen $col, $wd; $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLines($pen, [Drawing.PointF[]]($pts | ForEach-Object { New-Object Drawing.PointF $_[0], $_[1] })); $pen.Dispose()
}

# 학교 주변 공용 바탕(A02-M1~M3, B02-L1이 같은 도로 구조를 쓴다)
# 정문 (495,700) 큰길 쪽 / 후문 (760,262) 옆 골목 쪽 / 카페 (885,236) / 버스정류장 (905,778)
$Spot = @{ school = @(620, 435); main = @(495, 700); back = @(760, 262); cafe = @(885, 236); bus = @(905, 778) }
function School-Base {
    $c = New-Canvas 1080 1080 $M.ground; $g = $c.g
    $bb = New-Object Drawing.SolidBrush $M.block
    foreach ($r in @(@(40, 40, 230, 180), @(40, 300, 230, 380), @(860, 40, 180, 90), @(860, 300, 180, 400), @(40, 870, 300, 170), @(380, 870, 330, 170), @(760, 870, 280, 170), @(300, 40, 520, 70))) {
        Fill-RR $g $M.block $r[0] $r[1] $r[2] $r[3] 10
    }
    $bb.Dispose()
    # 카페 블록
    Fill-RR $g (C 'E8DCCB') 850 150 190 92 10
    # 학교 부지와 건물
    Fill-RR $g $M.green 300 140 460 560 18
    foreach ($r in @(@(350, 200, 170, 110), @(560, 200, 150, 200), @(350, 360, 120, 220), @(520, 470, 190, 120))) { Fill-RR $g $M.bld $r[0] $r[1] $r[2] $r[3] 8 }
    # 도로
    Road $g $M.road 34 @(@(0, 262), @(1080, 262))          # 위쪽 가로 골목은 짧게 쓰지 않고 이어둠
    Road $g $M.road 30 @(@(805, 0), @(805, 1080))          # 후문 옆 세로 골목
    Road $g $M.road 30 @(@(285, 0), @(285, 1080))          # 서쪽 세로 길
    Road $g $M.road 26 @(@(0, 830), @(1080, 830))
    $g.FillRectangle((New-Object Drawing.SolidBrush $M.bigEdge), 0, 732, 1080, 92)
    $g.FillRectangle((New-Object Drawing.SolidBrush $M.big), 0, 738, 1080, 80)  # 큰길
    foreach ($k in 0..13) { Line $g (C 'FFFFFF' 200) 4 (20 + $k * 80) 778 (60 + $k * 80) 778 }
    Road $g $M.road 34 @(@(0, 262), @(300, 262))
    # 정문·후문 표시(부지 경계의 틈)
    Fill-RR $g $M.road 465 690 60 22 6
    Fill-RR $g $M.road 748 236 24 52 6
    # 버스정류장 표시
    Fill-RR $g (C '7D8CA8') 880 742 50 22 6
    $c
}

$RouteCol = C '3E63C9'
function Route($g, $pts) {
    Road $g (C 'FFFFFF') 30 $pts
    Road $g $RouteCol 18 $pts
    $s = $pts[0]; $e = $pts[$pts.Count - 1]
    Fill-Circle $g (C 'FFFFFF') $s[0] $s[1] 22; Fill-Circle $g $RouteCol $s[0] $s[1] 15; Fill-Circle $g (C 'FFFFFF') $s[0] $s[1] 6
    Icon $g 'pin' $e[0] ($e[1] - 34) 80 (C 'FFFFFF'); Icon $g 'pin' $e[0] ($e[1] - 34) 70 (C 'C0574B')
}

# A02-M1: 장소 검색 결과(카페 핀 + 아래 장소 카드)
$c = School-Base; $g = $c.g
Icon $g 'pin' 945 160 96 (C 'FFFFFF'); Icon $g 'pin' 945 160 84 (C 'C0574B')
Fill-RR $g (C 'FFFFFF') 40 880 1000 170 24
Fill-Circle $g (C 'EDE6DD') 120 965 50; Icon $g 'cup' 120 965 56 $P.accent
Bar $g $P.text 200 930 320 26; Bar $g $P.sub 200 976 460 18
Save-Png $c (Join-Path $MapDir 'A_A02-M1.png')

# A02-M2: 학교 → 카페 도보(교내 → 후문 → 골목 건너 카페)
$c = School-Base; $g = $c.g
Route $g @($Spot.school, @(735, 435), @(735, 262), $Spot.back, @(805, 262), @(885, 262), $Spot.cafe)
Save-Png $c (Join-Path $MapDir 'A_A02-M2.png')

# A02-M3: 후문 → 버스정류장(옆 골목으로 내려가 정류장)
$c = School-Base; $g = $c.g
Route $g @($Spot.back, @(805, 262), @(805, 778), $Spot.bus)
Save-Png $c (Join-Path $MapDir 'A_A02-M3.png')

# B02-L1: 학교 → 버스정류장, 정문으로 나와 큰길을 따라감
$c = School-Base; $g = $c.g
Route $g @($Spot.school, @(495, 435), $Spot.main, @(495, 778), $Spot.bus)
Save-Png $c (Join-Path $MapDir 'B_B02-L1.png')

# 오래된 상가 공용 바탕(A_SH01-A, B_SH01-B)
function Arcade-Base {
    $c = New-Canvas 1080 1080 $M.ground; $g = $c.g
    foreach ($r in @(@(40, 40, 300, 250), @(400, 40, 260, 250), @(720, 40, 320, 250), @(40, 360, 240, 330), @(800, 360, 240, 330), @(40, 760, 420, 280), @(520, 760, 520, 280))) {
        Fill-RR $g $M.block $r[0] $r[1] $r[2] $r[3] 10
    }
    # 오래된 상가 건물(긴 블록 + 칸)
    Fill-RR $g (C 'D9CDBB') 340 380 400 300 10
    foreach ($k in 0..4) { Line $g (C 'C7B9A3') 3 (340 + $k * 80) 380 (340 + $k * 80) 680 }
    Line $g (C 'C7B9A3') 3 340 530 740 530
    Road $g $M.road 30 @(@(0, 325), @(1080, 325))
    Road $g $M.road 22 @(@(310, 325), @(310, 725))
    Road $g $M.road 22 @(@(770, 325), @(770, 725))
    $g.FillRectangle((New-Object Drawing.SolidBrush $M.bigEdge), 0, 694, 1080, 66)
    $g.FillRectangle((New-Object Drawing.SolidBrush $M.big), 0, 700, 1080, 54)
    Road $g $M.road 22 @(@(490, 760), @(490, 1080))
    $c
}

# 위치 기록: 두 기록 모두 같은 상가 건물 영역만 표시하고 기록별 색만 다르다.
# 도착 방향·이동 경로·건물 안의 정확한 위치는 기록에 없으므로 그리지 않는다.
function Stay($g, $col) {
    Fill-RR $g ([Drawing.Color]::FromArgb(70, $col)) 322 362 436 336 20
    Stroke-RR $g ([Drawing.Color]::FromArgb(200, $col)) 6 322 362 436 336 20
    Icon $g 'pin' 540 494 104 (C 'FFFFFF'); Icon $g 'pin' 540 494 92 $col
}
$c = Arcade-Base; Stay $c.g (C 'C0574B'); Save-Png $c (Join-Path $MapDir 'A_SH01-A.png')
$c = Arcade-Base; Stay $c.g (C '3E63C9'); Save-Png $c (Join-Path $MapDir 'B_SH01-B.png')
