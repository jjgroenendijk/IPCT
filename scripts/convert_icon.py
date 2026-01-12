import cairosvg
from PIL import Image
import io
import os

def convert_svg_to_ico(svg_path, ico_path):
    print(f"Converting {svg_path} to {ico_path}...")
    
    # Standard Windows icon sizes
    sizes = [16, 32, 48, 64, 128, 256]
    images = []

    for size in sizes:
        # Render SVG to PNG data in memory
        png_data = cairosvg.svg2png(url=svg_path, output_width=size, output_height=size)
        
        # Open with Pillow
        img = Image.open(io.BytesIO(png_data))
        images.append(img)
        print(f"Generated size: {size}x{size}")

    # Save as ICO
    # Sort by size descending (largest first)
    images.sort(key=lambda x: x.width, reverse=True)
    
    if images:
        images[0].save(
            ico_path, 
            format='ICO', 
            append_images=images[1:]
        )
        print("Success!")

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    svg_file = os.path.join(script_dir, "ipct.svg")
    ico_file = os.path.join(script_dir, "IPCT.ico")
    
    convert_svg_to_ico(svg_file, ico_file)
