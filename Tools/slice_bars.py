# -*- coding: utf-8 -*-
"""把血条 / 能量条素材切成 Unity 可用的「底图 + 填充条」，且对源图状态免疫。

无论源图是否自带预填充亮色，流程都是：
  1) 测出轨道区域（track）
  2) 底图：把整条轨道逐行刷成参考列的「空槽色」→ 0% 状态
  3) 填充条：裁同一段轨道，保留高光/阴影的明暗层次，换成饱满的属性色（红 / 蓝）

几何（源图 300x300，透明底；由像素测量得到）：
  血条   frame bbox (29,130)-(269,170)  轨道 x74-265  行 140-164
  能量条 frame bbox (30,115)-(267,174)  轨道 x66-263  行 141-166
"""
from PIL import Image
import os, numpy as np

SRC = r"D:\UNITY项目\YuansuPlan\Assets\Resources\Sprites\界面"

HP_FRAME = (29, 130, 269, 170)
HP_TRACK = (75, 140, 264, 165)      # 用于生成填充条（略去两端描边）
HP_REF = 230                        # 空槽参考列
EN_FRAME = (30, 115, 267, 174)
EN_TRACK = (67, 141, 263, 167)
EN_REF = 220

NAME_HP_SRC = "血条.png" if os.path.exists(os.path.join(SRC, "血条.png")) else "血条png.png"
NAME_EN_PNG = "能量条.png"
NAME_EN_WEBP = "能量条.webp"


def load(name):
    return Image.open(os.path.join(SRC, name)).convert("RGBA")


def ensure_energy_png():
    p = os.path.join(SRC, NAME_EN_PNG)
    if not os.path.exists(p) and os.path.exists(os.path.join(SRC, NAME_EN_WEBP)):
        load(NAME_EN_WEBP).save(p)
        print("能量条.webp -> 能量条.png")
    return NAME_EN_PNG


def repaint_empty(frame, track_box, ref_col_source, frame_box):
    """把轨道区逐行刷成参考列的空槽色（保留 alpha，图标与描边不动）。"""
    px = frame.load()
    ref = ref_col_source - frame_box[0]
    x0, y0, x1, y1 = [track_box[0] - frame_box[0], track_box[1] - frame_box[1],
                      track_box[2] - frame_box[0], track_box[3] - frame_box[1]]
    for y in range(y0, y1):
        r, g, b, a = px[ref, y]
        for x in range(x0, x1):
            _, _, _, aa = px[x, y]
            px[x, y] = (r, g, b, aa)
    return frame


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    idx = m & (mx == r); h[idx] = ((g[idx] - b[idx]) / d[idx]) % 6
    idx = m & (mx == g); h[idx] = (b[idx] - r[idx]) / d[idx] + 2
    idx = m & (mx == b); h[idx] = (r[idx] - g[idx]) / d[idx] + 4
    h = h / 6.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    i = (i % 6).astype(int)
    out = np.zeros(h.shape + (3,), dtype=np.float32)
    for k, (rr, gg, bb) in enumerate([(v, t, p), (q, v, p), (p, v, t),
                                      (p, q, v), (t, p, v), (v, p, q)]):
        m = i == k
        out[..., 0][m] = rr[m] if np.ndim(rr) else rr
        out[..., 1][m] = gg[m] if np.ndim(gg) else gg
        out[..., 2][m] = bb[m] if np.ndim(bb) else bb
    return out


def vivid_fill(frame, track_box, frame_box, target_rgb):
    """裁轨道区并上色：保留高光/阴影的明度层次，色相·饱和度换成本属性的饱满色。"""
    crop = frame.crop([track_box[0] - frame_box[0], track_box[1] - frame_box[1],
                       track_box[2] - frame_box[0], track_box[3] - frame_box[1]])
    rgb = np.array(crop.convert("RGB"), dtype=np.float32) / 255.0
    h, s, v = rgb_to_hsv(rgb)

    tgt = np.array([[target_rgb]], dtype=np.float32) / 255.0
    th, ts, tv = rgb_to_hsv(tgt)
    th, ts, tv = float(th[0, 0]), float(ts[0, 0]), float(tv[0, 0])

    s2 = np.clip(np.maximum(s, ts), 0.0, 1.0)          # 饱和度拉到目标色的水平
    v2 = np.clip(v * (tv / max(np.mean(v), 1e-6)) * 0.995 + 0.01, 0.0, 1.0)  # 明度整体校准到目标，保留纹理
    arr = np.clip(hsv_to_rgb(np.full_like(h, th), s2, v2) * 255, 0, 255).astype(np.uint8)
    res = Image.fromarray(arr, "RGB").convert("RGBA")
    res.putalpha(crop.split()[3])
    return res


def repair_alpha(im, track_box):
    """PS 擦除会让轨道变成半透明（alpha 20~60）。轨道内部只要 RGB 还有内容
    就把 alpha 恢复 255（描边、图标、圆头抗锯齿不在修复范围内，保持原样）。"""
    px = im.load()
    x0, y0, x1, y1 = track_box
    fixed = 0
    for y in range(y0 + 1, y1 - 1):
        for x in range(x0 + 1, x1 - 1):
            r, g, b, a = px[x, y]
            if a < 255 and r + g + b > 30:
                px[x, y] = (r, g, b, 255)
                fixed += 1
    if fixed:
        print("  repair_alpha: fixed %d px in track" % fixed)
    return im


def build(src_name, frame_box, track_box, ref, target, out_frame, out_fill):
    im = load(src_name)
    im = repair_alpha(im, track_box)
    frame = im.crop(frame_box)
    frame = repaint_empty(frame, track_box, ref, frame_box)
    frame.save(os.path.join(SRC, out_frame))
    fill = vivid_fill(frame, track_box, frame_box, target)
    fill.save(os.path.join(SRC, out_fill))
    print(out_frame, frame.size, "|", out_fill, fill.size)


if __name__ == "__main__":
    en = ensure_energy_png()
    build(NAME_HP_SRC, HP_FRAME, HP_TRACK, HP_REF, (244, 21, 42), "血条_底.png", "血条_填充.png")
    build(en, EN_FRAME, EN_TRACK, EN_REF, (51, 71, 246), "能量条_底.png", "能量条_填充.png")
    print("done")
