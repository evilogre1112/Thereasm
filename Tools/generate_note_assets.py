import math
import os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

OUTPUT_DIR = Path(r"d:\Project\Thereasm\Assets\UI\Note")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

# Colors from Thereasm palette
GOLD_LIGHT = (247, 212, 153, 255)    # #F7D499
GOLD_MID = (212, 168, 88, 255)      # #D4A858
GOLD_DARK = (140, 98, 38, 255)      # #8C6226
GOLD_DEEP = (85, 55, 18, 255)       # #553712

IVORY = (248, 236, 200, 255)        # #F8ECC8
IVORY_DARK = (210, 195, 158, 255)

OXBLOOD_BRIGHT = (138, 32, 32, 255) # #8A2020
OXBLOOD_MID = (90, 18, 18, 255)     # #5A1212
OXBLOOD_DARK = (54, 10, 10, 255)    # #360A0A
OXBLOOD_DEEP = (28, 6, 6, 255)      # #1C0606

CHARCOAL = (20, 16, 16, 255)        # #141010
CHARCOAL_DARK = (12, 9, 9, 255)
WOOD_DARK = (35, 23, 19, 255)       # #231713
WOOD_MID = (55, 38, 30, 255)

def draw_antialiased_polygon(img, points, fill, outline=None, width=1):
    draw = ImageDraw.Draw(img)
    draw.polygon(points, fill=fill, outline=outline, width=width)

def generate_note_icon(size=512):
    # Render at 2x for smooth supersampled antialiasing
    scale = 2
    W, H = size * scale, size * scale
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    
    # Book geometry
    bx0, by0 = int(W * 0.18), int(H * 0.12)
    bx1, by1 = int(W * 0.82), int(H * 0.82)
    radius = int(W * 0.04)
    spine_w = int(W * 0.09)
    
    # Shadow
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.rounded_rectangle([bx0 + 8, by0 + 16, bx1 + 12, by1 + 24], radius=radius, fill=(0, 0, 0, 120))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=int(12 * scale)))
    canvas.alpha_composite(shadow)
    
    # Hanging Bookmark Ribbon at bottom
    rx0 = int(W * 0.52)
    rx1 = int(W * 0.62)
    ry0 = int(H * 0.70)
    ry1 = int(H * 0.94)
    ribbon_pts = [
        (rx0, ry0),
        (rx1, ry0),
        (rx1, ry1),
        (int((rx0 + rx1) / 2), ry1 - int(H * 0.04)),
        (rx0, ry1)
    ]
    draw.polygon(ribbon_pts, fill=IVORY, outline=CHARCOAL, width=int(2 * scale))
    # Ribbon golden tip seal
    seal_y = ry1 - int(H * 0.07)
    draw.ellipse([int((rx0+rx1)/2 - 10*scale), seal_y - 10*scale, int((rx0+rx1)/2 + 10*scale), seal_y + 10*scale], fill=GOLD_MID, outline=CHARCOAL, width=int(1.5*scale))

    # Pages block (visible bottom and right edge)
    draw.rounded_rectangle([bx0 + spine_w, by0 + 6*scale, bx1 + 6*scale, by1 + 6*scale], radius=radius, fill=IVORY_DARK, outline=CHARCOAL, width=int(2 * scale))
    # Page lines
    for off in range(2, 6):
        draw.line([(bx1 + off*scale, by0 + int(H*0.05)), (bx1 + off*scale, by1)], fill=CHARCOAL, width=int(1*scale))

    # Leather Cover (Oxblood gradient)
    cover = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    c_draw = ImageDraw.Draw(cover)
    c_draw.rounded_rectangle([bx0, by0, bx1, by1], radius=radius, fill=OXBLOOD_DARK)
    
    # Shading across the cover
    for y in range(by0, by1):
        ratio = (y - by0) / (by1 - by0)
        col = (
            int(OXBLOOD_BRIGHT[0] * (1 - ratio*0.5)),
            int(OXBLOOD_BRIGHT[1] * (1 - ratio*0.5)),
            int(OXBLOOD_BRIGHT[2] * (1 - ratio*0.5)),
            255
        )
        c_draw.line([(bx0, y), (bx1, y)], fill=col)

    # Leather border embossing
    emb_pad = int(W * 0.04)
    c_draw.rounded_rectangle([bx0 + emb_pad, by0 + emb_pad, bx1 - emb_pad, by1 - emb_pad], radius=int(radius*0.7), outline=OXBLOOD_DEEP, width=int(3 * scale))
    c_draw.rounded_rectangle([bx0 + emb_pad + 2*scale, by0 + emb_pad + 2*scale, bx1 - emb_pad - 2*scale, by1 - emb_pad - 2*scale], radius=int(radius*0.7), outline=GOLD_DEEP, width=int(1 * scale))

    # Mask cover to rounded rect
    mask = Image.new("L", (W, H), 0)
    m_draw = ImageDraw.Draw(mask)
    m_draw.rounded_rectangle([bx0, by0, bx1, by1], radius=radius, fill=255)
    canvas.paste(cover, (0, 0), mask)
    
    # Spine (Left)
    spine = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sp_draw = ImageDraw.Draw(spine)
    sp_draw.rounded_rectangle([bx0, by0, bx0 + spine_w, by1], radius=int(radius*0.5), fill=OXBLOOD_DEEP, outline=CHARCOAL, width=int(2*scale))
    # Spine metal bands
    for fy in [0.22, 0.40, 0.58, 0.74]:
        sby = int(by0 + (by1 - by0) * fy)
        sp_draw.rectangle([bx0 - 2*scale, sby - 5*scale, bx0 + spine_w + 2*scale, sby + 5*scale], fill=GOLD_MID, outline=CHARCOAL, width=int(1.5*scale))
        sp_draw.ellipse([bx0 + spine_w//2 - 3*scale, sby - 3*scale, bx0 + spine_w//2 + 3*scale, sby + 3*scale], fill=GOLD_LIGHT)
    canvas.alpha_composite(spine)

    # Brass Corner Brackets
    def draw_corner(cx, cy, flip_x, flip_y):
        pts = [
            (cx, cy),
            (cx + (int(W * 0.12) * flip_x), cy),
            (cx + (int(W * 0.12) * flip_x), cy + (int(H * 0.04) * flip_y)),
            (cx + (int(H * 0.04) * flip_x), cy + (int(W * 0.12) * flip_y)),
            (cx, cy + (int(W * 0.12) * flip_y))
        ]
        draw.polygon(pts, fill=GOLD_MID, outline=CHARCOAL, width=int(2 * scale))
        # Inner rivet
        rx = cx + (int(W * 0.045) * flip_x)
        ry = cy + (int(H * 0.045) * flip_y)
        draw.ellipse([rx - 3*scale, ry - 3*scale, rx + 3*scale, ry + 3*scale], fill=GOLD_LIGHT, outline=CHARCOAL, width=int(1*scale))

    draw_corner(bx0 + spine_w - 4*scale, by0, 1, 1)          # Top left (from spine)
    draw_corner(bx1, by0, -1, 1)                             # Top right
    draw_corner(bx0 + spine_w - 4*scale, by1, 1, -1)         # Bottom left
    draw_corner(bx1, by1, -1, -1)                            # Bottom right

    # Ornate Clasp / Lock on right side
    clasp_y = int((by0 + by1) / 2)
    clasp_w = int(W * 0.07)
    clasp_h = int(H * 0.14)
    clasp_pts = [
        (bx1 - int(W * 0.03), clasp_y - clasp_h//2),
        (bx1 + clasp_w, clasp_y - clasp_h//3),
        (bx1 + clasp_w + int(W * 0.02), clasp_y),
        (bx1 + clasp_w, clasp_y + clasp_h//3),
        (bx1 - int(W * 0.03), clasp_y + clasp_h//2)
    ]
    draw.polygon(clasp_pts, fill=GOLD_MID, outline=CHARCOAL, width=int(2 * scale))
    draw.ellipse([bx1 + clasp_w - 6*scale, clasp_y - 6*scale, bx1 + clasp_w + 6*scale, clasp_y + 6*scale], fill=CHARCOAL)
    draw.ellipse([bx1 + clasp_w - 2*scale, clasp_y - 2*scale, bx1 + clasp_w + 2*scale, clasp_y + 2*scale], fill=GOLD_LIGHT)

    # Center Occult Insignia (Golden Eye & Crescent Moon)
    cx = int(bx0 + spine_w + (bx1 - bx0 - spine_w) / 2)
    cy = int((by0 + by1) / 2)
    
    # Outer radiating ring
    draw.ellipse([cx - int(W * 0.15), cy - int(W * 0.15), cx + int(W * 0.15), cy + int(W * 0.15)], outline=GOLD_DEEP, width=int(1.5*scale))
    draw.ellipse([cx - int(W * 0.14), cy - int(W * 0.14), cx + int(W * 0.14), cy + int(W * 0.14)], outline=GOLD_MID, width=int(2*scale))
    
    # Crescent moon
    m_rad = int(W * 0.11)
    draw.ellipse([cx - m_rad, cy - m_rad, cx + m_rad, cy + m_rad], fill=GOLD_LIGHT, outline=CHARCOAL, width=int(1.5*scale))
    draw.ellipse([cx - m_rad + int(W * 0.035), cy - m_rad - int(H * 0.01), cx + m_rad + int(W * 0.035), cy + m_rad - int(H * 0.01)], fill=OXBLOOD_DARK)
    
    # Mystic Eye in center
    eye_w = int(W * 0.08)
    eye_h = int(H * 0.045)
    eye_pts_top = [(cx - eye_w, cy)]
    eye_pts_bot = []
    for step in range(1, 11):
        t = step / 10.0
        x = cx - eye_w + t * (2 * eye_w)
        yt = cy - math.sin(t * math.pi) * eye_h
        yb = cy + math.sin(t * math.pi) * eye_h
        eye_pts_top.append((x, yt))
        eye_pts_bot.append((x, yb))
    eye_pts = eye_pts_top + list(reversed(eye_pts_bot))
    draw.polygon(eye_pts, fill=IVORY, outline=CHARCOAL, width=int(2*scale))
    # Pupil
    draw.ellipse([cx - int(W * 0.02), cy - int(H * 0.02), cx + int(W * 0.02), cy + int(H * 0.02)], fill=GOLD_DARK, outline=CHARCOAL, width=int(1*scale))
    draw.ellipse([cx - int(W * 0.008), cy - int(H * 0.008), cx + int(W * 0.008), cy + int(H * 0.008)], fill=CHARCOAL)

    # 4 cardinal stars
    for angle in [0, math.pi/2, math.pi, 3*math.pi/2]:
        sx = cx + math.cos(angle) * (W * 0.15)
        sy = cy + math.sin(angle) * (H * 0.15)
        draw.ellipse([sx - 3*scale, sy - 3*scale, sx + 3*scale, sy + 3*scale], fill=GOLD_LIGHT)

    # Outline whole book
    draw.rounded_rectangle([bx0, by0, bx1, by1], radius=radius, outline=CHARCOAL, width=int(2.5 * scale))

    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)
    final_img.save(OUTPUT_DIR / "note-icon.png", "PNG")
    print(f"Generated note-icon.png ({size}x{size})")

def generate_note_token(size=512):
    scale = 2
    W, H = size * scale, size * scale
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    
    cx, cy = W // 2, H // 2
    R = int(W * 0.44)
    
    # Soft drop shadow
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.ellipse([cx - R, cy - R + 14*scale, cx + R, cy + R + 14*scale], fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=int(10 * scale)))
    canvas.alpha_composite(shadow)
    
    # Outer dark charcoal border
    draw.ellipse([cx - R, cy - R, cx + R, cy + R], fill=CHARCOAL)
    
    # Beveled Antique Brass Rim (concentric shaded rings)
    rim_layers = [
        (0.98, GOLD_DARK),
        (0.96, GOLD_MID),
        (0.92, GOLD_LIGHT),
        (0.86, GOLD_MID),
        (0.82, GOLD_DARK),
        (0.78, GOLD_DEEP),
        (0.75, CHARCOAL)
    ]
    for frac, col in rim_layers:
        r_cur = int(R * frac)
        draw.ellipse([cx - r_cur, cy - r_cur, cx + r_cur, cy + r_cur], fill=col)
        
    # 8 decorative engraved rivets around the brass rim
    rivet_r = int(R * 0.89)
    for i in range(12):
        angle = (i / 12.0) * 2 * math.pi
        rx = cx + math.cos(angle) * rivet_r
        ry = cy + math.sin(angle) * rivet_r
        draw.ellipse([rx - 6*scale, ry - 6*scale, rx + 6*scale, ry + 6*scale], fill=CHARCOAL)
        draw.ellipse([rx - 4*scale, ry - 4*scale, rx + 4*scale, ry + 4*scale], fill=GOLD_LIGHT)
        draw.ellipse([rx - 1*scale, ry - 1*scale, rx + 1*scale, ry + 1*scale], fill=GOLD_DEEP)

    # Recessed Oxblood / Dark Mahogany Center Disc
    r_center = int(R * 0.74)
    # Radial dark red fill
    for rad_step in range(r_center, 0, -2*scale):
        t = rad_step / float(r_center)
        # Deep burgundy to dark center
        cr = int(OXBLOOD_DARK[0] * (0.8 + 0.5 * (1 - t)))
        cg = int(OXBLOOD_DARK[1] * (0.8 + 0.4 * (1 - t)))
        cb = int(OXBLOOD_DARK[2] * (0.8 + 0.4 * (1 - t)))
        draw.ellipse([cx - rad_step, cy - rad_step, cx + rad_step, cy + rad_step], fill=(cr, cg, cb, 255))
        
    # Inner delicate gold hairline ring
    r_hairline = int(R * 0.68)
    draw.ellipse([cx - r_hairline, cy - r_hairline, cx + r_hairline, cy + r_hairline], outline=GOLD_DEEP, width=int(2*scale))
    draw.ellipse([cx - r_hairline + 2*scale, cy - r_hairline + 2*scale, cx + r_hairline - 2*scale, cy + r_hairline - 2*scale], outline=GOLD_MID, width=int(1.5*scale))
    
    # 4 subtle corner knot flourishes at 45 degree angles inside the hairline
    for i in range(4):
        ang = (i * math.pi / 2) + (math.pi / 4)
        kx = cx + math.cos(ang) * (r_hairline - 14*scale)
        ky = cy + math.sin(ang) * (r_hairline - 14*scale)
        draw.ellipse([kx - 4*scale, ky - 4*scale, kx + 4*scale, ky + 4*scale], fill=GOLD_MID, outline=CHARCOAL, width=int(1*scale))

    # Leave the center empty for TMP numbers.
    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)
    final_img.save(OUTPUT_DIR / "note-token.png", "PNG")
    print(f"Generated note-token.png ({size}x{size})")

def generate_note_slot(size=512):
    scale = 2
    W, H = size * scale, size * scale
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    
    pad = int(W * 0.05)
    x0, y0 = pad, pad
    x1, y1 = W - pad, H - pad
    r = int(W * 0.05)
    
    # Outer dark charcoal border
    draw.rounded_rectangle([x0, y0, x1, y1], radius=r, fill=WOOD_DARK, outline=CHARCOAL, width=int(2*scale))
    
    # Wooden frame layer
    draw.rounded_rectangle([x0 + 4*scale, y0 + 4*scale, x1 - 4*scale, y1 - 4*scale], radius=int(r*0.8), fill=WOOD_MID)
    
    # Inset Recessed Velvet / Charcoal Well
    well_pad = int(W * 0.08)
    draw.rounded_rectangle([x0 + well_pad, y0 + well_pad, x1 - well_pad, y1 - well_pad], radius=int(r*0.6), fill=CHARCOAL_DARK)
    
    # Well depth inner shadow (top and left edges darker)
    w0, h0 = x0 + well_pad, y0 + well_pad
    w1, h1 = x1 - well_pad, y1 - well_pad
    draw.rounded_rectangle([w0, h0, w1, h1], radius=int(r*0.6), outline=OXBLOOD_DEEP, width=int(3*scale))
    
    # Subtle antique crosshair / target watermark in the empty slot
    cx, cy = W // 2, H // 2
    cross_sz = int(W * 0.16)
    draw.line([(cx - cross_sz, cy), (cx + cross_sz, cy)], fill=(120, 85, 45, 90), width=int(1.5*scale))
    draw.line([(cx, cy - cross_sz), (cx, cy + cross_sz)], fill=(120, 85, 45, 90), width=int(1.5*scale))
    draw.ellipse([cx - int(W*0.08), cy - int(W*0.08), cx + int(W*0.08), cy + int(W*0.08)], outline=(120, 85, 45, 75), width=int(1.5*scale))

    # Ornate Brass L-brackets on all 4 corners
    corner_sz = int(W * 0.18)
    c_thick = int(W * 0.05)
    
    def draw_corner_bracket(bx, by, dx, dy):
        # Outer L shape
        pts = [
            (bx, by),
            (bx + dx * corner_sz, by),
            (bx + dx * corner_sz, by + dy * c_thick),
            (bx + dx * c_thick, by + dy * c_thick),
            (bx + dx * c_thick, by + dy * corner_sz),
            (bx, by + dy * corner_sz)
        ]
        draw.polygon(pts, fill=GOLD_MID, outline=CHARCOAL, width=int(2*scale))
        # Highlight edge
        draw.line([(bx + dx * 2*scale, by + dy * 2*scale), (bx + dx * (corner_sz - 4*scale), by + dy * 2*scale)], fill=GOLD_LIGHT, width=int(1.5*scale))
        draw.line([(bx + dx * 2*scale, by + dy * 2*scale), (bx + dx * 2*scale, by + dy * (corner_sz - 4*scale))], fill=GOLD_LIGHT, width=int(1.5*scale))
        # Corner Rivet
        rx = bx + dx * int(c_thick * 0.5)
        ry = by + dy * int(c_thick * 0.5)
        draw.ellipse([rx - 3*scale, ry - 3*scale, rx + 3*scale, ry + 3*scale], fill=GOLD_LIGHT, outline=CHARCOAL, width=int(1*scale))

    draw_corner_bracket(x0 + 2*scale, y0 + 2*scale, 1, 1)
    draw_corner_bracket(x1 - 2*scale, y0 + 2*scale, -1, 1)
    draw_corner_bracket(x0 + 2*scale, y1 - 2*scale, 1, -1)
    draw_corner_bracket(x1 - 2*scale, y1 - 2*scale, -1, -1)

    # Frame outer outline
    draw.rounded_rectangle([x0, y0, x1, y1], radius=r, outline=CHARCOAL, width=int(2*scale))

    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)
    final_img.save(OUTPUT_DIR / "note-slot.png", "PNG")
    print(f"Generated note-slot.png ({size}x{size})")

def generate_note_divider(width=1024, height=64):
    scale = 2
    W, H = width * scale, height * scale
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    
    cx, cy = W // 2, H // 2
    
    # Main horizontal tapering lines
    half_len = int(W * 0.44)
    for off in range(half_len):
        t = off / float(half_len) # 0 at center, 1 at ends
        thickness = max(1.0, (1.0 - t) * 3.5 * scale)
        alpha = int(255 * (1.0 - t**2))
        col = (GOLD_MID[0], GOLD_MID[1], GOLD_MID[2], alpha)
        
        # Left wing
        draw.line([(cx - off, cy), (cx - off, cy)], fill=col, width=int(thickness))
        # Right wing
        draw.line([(cx + off, cy), (cx + off, cy)], fill=col, width=int(thickness))

    # Secondary delicate filigree beads along top and bottom
    for step in range(10, half_len - int(W*0.05), int(20*scale)):
        t = step / float(half_len)
        r_bead = max(1*scale, int((1 - t) * 3.5 * scale))
        alpha = int(220 * (1 - t))
        col = (GOLD_LIGHT[0], GOLD_LIGHT[1], GOLD_LIGHT[2], alpha)
        draw.ellipse([cx - step - r_bead, cy - r_bead, cx - step + r_bead, cy + r_bead], fill=col)
        draw.ellipse([cx + step - r_bead, cy - r_bead, cx + step + r_bead, cy + r_bead], fill=col)

    # Central Occult Medallion
    dia_r = int(H * 0.38)
    diamond = [
        (cx, cy - dia_r),
        (cx + int(dia_r * 1.5), cy),
        (cx, cy + dia_r),
        (cx - int(dia_r * 1.5), cy)
    ]
    draw.polygon(diamond, fill=GOLD_DEEP, outline=CHARCOAL, width=int(2*scale))
    
    # Inner gold diamond
    dia_inner = [
        (cx, cy - int(dia_r * 0.7)),
        (cx + int(dia_r * 1.1), cy),
        (cx, cy + int(dia_r * 0.7)),
        (cx - int(dia_r * 1.1), cy)
    ]
    draw.polygon(dia_inner, fill=GOLD_LIGHT, outline=GOLD_MID, width=int(1.5*scale))
    
    # Center crimson occult jewel
    draw.ellipse([cx - int(dia_r*0.4), cy - int(dia_r*0.4), cx + int(dia_r*0.4), cy + int(dia_r*0.4)], fill=OXBLOOD_BRIGHT, outline=CHARCOAL, width=int(1.5*scale))
    draw.ellipse([cx - 2*scale, cy - 2*scale, cx + 2*scale, cy + 2*scale], fill=GOLD_LIGHT)

    final_img = canvas.resize((width, height), Image.Resampling.LANCZOS)
    final_img.save(OUTPUT_DIR / "note-divider.png", "PNG")
    print(f"Generated note-divider.png ({width}x{height})")

def generate_close_icon(size=256):
    scale = 2
    W, H = size * scale, size * scale
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)
    
    cx, cy = W // 2, H // 2
    R = int(W * 0.44)
    
    # Shadow
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    s_draw = ImageDraw.Draw(shadow)
    s_draw.ellipse([cx - R, cy - R + 8*scale, cx + R, cy + R + 8*scale], fill=(0, 0, 0, 140))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=int(6 * scale)))
    canvas.alpha_composite(shadow)
    
    # Outer brass rim
    draw.ellipse([cx - R, cy - R, cx + R, cy + R], fill=GOLD_DEEP, outline=CHARCOAL, width=int(2*scale))
    draw.ellipse([cx - int(R*0.95), cy - int(R*0.95), cx + int(R*0.95), cy + int(R*0.95)], fill=GOLD_MID)
    draw.ellipse([cx - int(R*0.86), cy - int(R*0.86), cx + int(R*0.86), cy + int(R*0.86)], fill=GOLD_LIGHT)
    draw.ellipse([cx - int(R*0.80), cy - int(R*0.80), cx + int(R*0.80), cy + int(R*0.80)], fill=OXBLOOD_DARK, outline=CHARCOAL, width=int(2*scale))
    
    # Carved '✕'
    x_sz = int(R * 0.44)
    x_thick = int(6 * scale)
    # Highlight
    draw.line([(cx - x_sz, cy - x_sz), (cx + x_sz, cy + x_sz)], fill=IVORY, width=x_thick)
    draw.line([(cx + x_sz, cy - x_sz), (cx - x_sz, cy + x_sz)], fill=IVORY, width=x_thick)
    # Inner gold line
    draw.line([(cx - x_sz, cy - x_sz), (cx + x_sz, cy + x_sz)], fill=GOLD_LIGHT, width=int(3*scale))
    draw.line([(cx + x_sz, cy - x_sz), (cx - x_sz, cy + x_sz)], fill=GOLD_LIGHT, width=int(3*scale))

    final_img = canvas.resize((size, size), Image.Resampling.LANCZOS)
    final_img.save(OUTPUT_DIR / "note-close-icon.png", "PNG")
    print(f"Generated note-close-icon.png ({size}x{size})")

if __name__ == "__main__":
    generate_note_icon(1024)
    generate_note_token(1024)
    generate_note_slot(1024)
    generate_note_divider(1024, 128)
    generate_close_icon(512)
    print("All note UI assets generated in ultra-crisp resolution!")
