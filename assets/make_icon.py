from PIL import Image, ImageDraw
import os

def create_soundcloud_icon(output_path: str):
    size = 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Rounded square background with vertical orange gradient
    radius = 110
    bg = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bg_draw = ImageDraw.Draw(bg)
    for y in range(size):
        t = y / (size - 1)
        r = int(255)
        g = int(122 * (1 - t) + 51 * t)   # #FF7A00 -> #FF3300
        b = int(0)
        bg_draw.line([(0, y), (size, y)], fill=(r, g, b, 255))

    mask = Image.new("L", (size, size), 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.rounded_rectangle([16, 16, size - 16, size - 16], radius=radius, fill=255)
    img.paste(bg, (0, 0), mask)

    # Draw stylized white SoundCloud cloud + vertical waveform bars on left
    white = (255, 255, 255, 255)
    cloud = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    cd = ImageDraw.Draw(cloud)

    # Left vertical bars (SoundCloud signature stripes)
    bar_w = 18
    gap = 12
    # (x, top, bottom)
    bars = [
        (86,  276, 338),
        (116, 238, 344),
        (146, 252, 346),
        (176, 210, 346),
        (206, 224, 346),
        (236, 174, 346),
    ]
    for x, top, bottom in bars:
        cd.rounded_rectangle([x, top, x + bar_w, bottom], radius=9, fill=white)

    # Right solid cloud body
    # Base rectangle + circles for smooth cloud silhouette
    base_left = 266
    base_right = 374
    base_bottom = 346
    base_top = 248
    cd.rectangle([base_left, base_top, base_right, base_bottom], fill=white)

    # Main top dome
    cd.ellipse([252, 156, 396, 300], fill=white)
    # Right lower dome
    cd.ellipse([326, 232, 438, 346], fill=white)
    # Connect bottom edge flat
    cd.rectangle([base_left, 286, 382, base_bottom], fill=white)

    img = Image.alpha_composite(img, cloud)

    icon_sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]
    img.save(output_path, format="ICO", sizes=icon_sizes)
    png_path = os.path.splitext(output_path)[0] + ".png"
    img.resize((256, 256), Image.Resampling.LANCZOS).save(png_path, format="PNG")
    print(f"Generated {output_path} and {png_path}")

if __name__ == "__main__":
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "soundcloud.ico")
    create_soundcloud_icon(out)
