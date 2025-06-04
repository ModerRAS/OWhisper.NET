#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图标处理脚本 - 智能去除背景白色，保护麦克风内部白色，并进行智能裁切
"""

from PIL import Image, ImageFilter
import numpy as np
import os

def create_mask_for_microphone(img):
    """
    创建一个遮罩来保护麦克风内部的白色部分
    """
    # 转换为numpy数组进行处理
    img_array = np.array(img)
    height, width = img_array.shape[:2]
    
    # 创建遮罩，标记麦克风内部区域
    mask = np.zeros((height, width), dtype=bool)
    
    # 找到图像中心
    center_x, center_y = width // 2, height // 2
    
    # 麦克风主体大概占图像的40-70%区域
    mic_radius = min(width, height) * 0.35
    
    # 标记麦克风内部区域（圆形区域）
    for y in range(height):
        for x in range(width):
            # 计算到中心的距离
            distance_to_center = np.sqrt((x - center_x)**2 + (y - center_y)**2)
            
            # 如果在麦克风内部区域
            if distance_to_center < mic_radius:
                mask[y, x] = True
    
    return mask

def smart_background_removal(img):
    """
    智能背景去除，保护麦克风内部白色
    """
    print("正在进行智能背景去除...")
    
    # 转换为RGBA模式
    if img.mode != 'RGBA':
        img = img.convert('RGBA')
    
    # 转换为numpy数组
    img_array = np.array(img)
    height, width = img_array.shape[:2]
    
    # 创建麦克风内部保护遮罩
    mic_mask = create_mask_for_microphone(img)
    
    # 创建新的图像数据
    new_img_array = img_array.copy()
    
    # 分析边缘，找出背景色
    edge_pixels = []
    edge_thickness = max(5, min(width, height) // 100)  # 边缘厚度
    
    # 收集边缘像素用于背景分析
    for i in range(edge_thickness):
        # 上边缘
        edge_pixels.extend(img_array[i, :, :3])
        # 下边缘
        edge_pixels.extend(img_array[height-1-i, :, :3])
        # 左边缘
        edge_pixels.extend(img_array[:, i, :3])
        # 右边缘
        edge_pixels.extend(img_array[:, width-1-i, :3])
    
    edge_pixels = np.array(edge_pixels)
    
    # 计算边缘的平均颜色（作为背景色参考）
    bg_color = np.mean(edge_pixels, axis=0)
    print(f"检测到的背景色: RGB({bg_color[0]:.1f}, {bg_color[1]:.1f}, {bg_color[2]:.1f})")
    
    # 处理每个像素
    for y in range(height):
        for x in range(width):
            pixel = img_array[y, x]
            r, g, b = pixel[0], pixel[1], pixel[2]
            
            # 如果在麦克风内部保护区域，跳过
            if mic_mask[y, x]:
                continue
            
            # 计算像素与背景色的相似度
            color_diff = np.sqrt(
                (r - bg_color[0])**2 + 
                (g - bg_color[1])**2 + 
                (b - bg_color[2])**2
            )
            
            # 计算到图像边缘的最小距离
            edge_distance = min(x, y, width-1-x, height-1-y)
            edge_factor = max(0, 1 - edge_distance / (min(width, height) * 0.2))
            
            # 背景判断条件：
            # 1. 颜色相似度高（接近背景色）
            # 2. 或者是边缘附近的浅色像素
            # 3. 或者是非常接近白色的像素（但不在麦克风内部）
            is_background = (
                color_diff < 50 or  # 与背景色相似
                (edge_factor > 0.5 and r > 200 and g > 200 and b > 200) or  # 边缘浅色
                (r > 250 and g > 250 and b > 250)  # 非常白的像素
            )
            
            if is_background:
                # 设为透明
                new_img_array[y, x, 3] = 0
    
    # 转换回PIL图像
    result_img = Image.fromarray(new_img_array, 'RGBA')
    
    # 轻微的反锯齿处理
    result_img = result_img.filter(ImageFilter.SMOOTH_MORE)
    
    return result_img

def crop_transparent_edges(img, padding_ratio=0.05):
    """
    裁切透明边缘，保留一点padding，让图标更大
    
    Args:
        img: PIL图像对象
        padding_ratio: 保留的边距比例（相对于原图尺寸）
    """
    print("正在进行智能裁切，去除多余的透明边缘...")
    
    if img.mode != 'RGBA':
        return img
    
    img_array = np.array(img)
    alpha_channel = img_array[:, :, 3]
    
    # 找到非透明像素的边界
    non_transparent = alpha_channel > 0
    
    if not np.any(non_transparent):
        print("警告: 图像完全透明，跳过裁切")
        return img
    
    # 找到边界
    rows = np.any(non_transparent, axis=1)
    cols = np.any(non_transparent, axis=0)
    
    if not np.any(rows) or not np.any(cols):
        print("警告: 无法找到有效边界，跳过裁切")
        return img
    
    top, bottom = np.where(rows)[0][[0, -1]]
    left, right = np.where(cols)[0][[0, -1]]
    
    # 添加padding
    height, width = img_array.shape[:2]
    padding_x = int(width * padding_ratio)
    padding_y = int(height * padding_ratio)
    
    # 确保边界在图像范围内
    top = max(0, top - padding_y)
    bottom = min(height - 1, bottom + padding_y)
    left = max(0, left - padding_x)
    right = min(width - 1, right + padding_x)
    
    print(f"原始尺寸: {width}x{height}")
    print(f"裁切区域: ({left}, {top}) -> ({right}, {bottom})")
    print(f"裁切后尺寸: {right-left+1}x{bottom-top+1}")
    
    # 裁切图像
    cropped_img = img.crop((left, top, right + 1, bottom + 1))
    
    return cropped_img

def process_icon(input_path, output_dir):
    """
    处理图标：智能背景去除、裁切并生成高分辨率的ICO文件
    """
    print(f"正在处理图标: {input_path}")
    
    # 确保输出目录存在
    os.makedirs(output_dir, exist_ok=True)
    
    # 打开图像
    img = Image.open(input_path)
    print(f"原始图像: {img.size} - {img.mode}")
    
    # 进行智能背景去除
    transparent_img = smart_background_removal(img)
    
    # 进行智能裁切，让图标更大
    cropped_img = crop_transparent_edges(transparent_img, padding_ratio=0.03)  # 减少padding让图标更大
    
    # 保存透明PNG
    png_path = os.path.join(output_dir, 'app_icon_transparent.png')
    cropped_img.save(png_path, 'PNG')
    print(f"透明PNG保存到: {png_path}")
    
    # 创建高分辨率图标 - 使用更高的分辨率
    sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512]  # 添加512x512
    
    # 为ICO格式准备图像列表
    icon_images = []
    
    for size in sizes:
        # 使用高质量的LANCZOS重采样
        resized = cropped_img.resize((size, size), Image.Resampling.LANCZOS)
        
        # 对于高分辨率图标，进行额外的锐化处理
        if size >= 64:
            resized = resized.filter(ImageFilter.UnsharpMask(radius=0.5, percent=120, threshold=3))
        
        icon_images.append(resized)
        
        # 保存单独的PNG文件
        size_png_path = os.path.join(output_dir, f'app_icon_{size}x{size}.png')
        resized.save(size_png_path, 'PNG')
        print(f"创建 {size}x{size} PNG: {size_png_path}")
    
    # 保存ICO文件（包含所有尺寸）
    ico_path = os.path.join(output_dir, 'app_icon.ico')
    icon_images[0].save(
        ico_path, 
        format='ICO', 
        sizes=[(size, size) for size in sizes]
    )
    print(f"ICO文件保存到: {ico_path}")
    
    # 创建专门用于托盘的超高分辨率图标
    tray_sizes = [32, 48, 64, 96, 128, 256]  # 包含更高分辨率
    tray_images = []
    
    for size in tray_sizes:
        resized = cropped_img.resize((size, size), Image.Resampling.LANCZOS)
        
        # 对于托盘图标进行锐化处理
        if size >= 48:
            resized = resized.filter(ImageFilter.UnsharpMask(radius=0.3, percent=100, threshold=2))
        
        tray_images.append(resized)
    
    # 保存托盘专用ICO
    tray_ico_path = os.path.join(output_dir, 'app_tray_icon.ico')
    tray_images[0].save(
        tray_ico_path,
        format='ICO',
        sizes=[(size, size) for size in tray_sizes]
    )
    print(f"托盘ICO文件保存到: {tray_ico_path}")
    
    # 创建超高分辨率版本用于特殊需求
    super_high_res_sizes = [256, 512]
    for size in super_high_res_sizes:
        super_resized = cropped_img.resize((size, size), Image.Resampling.LANCZOS)
        # 高分辨率锐化
        super_resized = super_resized.filter(ImageFilter.UnsharpMask(radius=1.0, percent=150, threshold=3))
        
        super_path = os.path.join(output_dir, f'app_icon_super_{size}x{size}.png')
        super_resized.save(super_path, 'PNG')
        print(f"创建超高分辨率 {size}x{size} PNG: {super_path}")
    
    print("图标处理完成！")
    print(f"生成了 {len(sizes)} 种通用尺寸、{len(tray_sizes)} 种托盘尺寸和 {len(super_high_res_sizes)} 种超高分辨率")
    print("💡 图标已经过裁切处理，在显示时会更大更清晰！")

def main():
    # 输入和输出路径
    input_file = 'icon.png'
    output_directory = 'OWhisper.NET/Resources'
    
    if not os.path.exists(input_file):
        print(f"错误: 找不到输入文件 {input_file}")
        return
    
    try:
        process_icon(input_file, output_directory)
        print("\n✅ 处理完成！现在可以查看生成的高分辨率图标了。")
        print("🔍 提示: 图标现在支持512x512分辨率，并且经过裁切处理，显示时会更大更清晰！")
        print("🎯 托盘图标在Windows 11高DPI显示器上会非常清晰。")
    except Exception as e:
        print(f"处理图标时出错: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main() 