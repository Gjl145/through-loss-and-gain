#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""NoiseFish 编译前静态自检（沙箱无 Unity，用它替代人工编译前的第一道关）。

用法:
    python .workbuddy/tools/cscheck.py            # 检查全部脚本
    python .workbuddy/tools/cscheck.py --baseline # 与上次记录的行数基线对比

退出码: 0 = 通过；1 = 发现问题。
"""
import re, sys, os, json, argparse

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPT_DIR = os.path.join(ROOT, "Assets", "Scripts")
EDITOR_DIR = os.path.join(ROOT, "Assets", "Editor")
BASELINE = os.path.join(ROOT, ".workbuddy", "tools", "line_baseline.json")

# 需要检查的源文件
def collect():
    files = []
    for d in (SCRIPT_DIR, EDITOR_DIR):
        if not os.path.isdir(d):
            continue
        for name in sorted(os.listdir(d)):
            if name.endswith(".cs"):
                files.append(os.path.join(d, name))
    return files


def strip_code(src):
    """剔除字符串/字符/注释，只用于结构检查（不改文件）。"""
    s = re.sub(r'"(\\.|[^"\\])*"', '""', src)
    s = re.sub(r"'(\\.|[^'\\])*'", "''", s)
    s = re.sub(r'//.*', '', s)
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    return s


# Unity 常见误用 → 正确写法
BAD_API = {
    r'\bInput\.mouseScrollWheel\b':
        'Unity 6 已移除 Input.mouseScrollWheel，改用 Input.GetAxis("Mouse ScrollWheel")',
    r'\bApplication\.LoadLevel\b':
        '旧 API，改用 SceneManager.LoadScene',
    r'\bSphereRenderer\b':
        'Unity 没有 SphereRenderer 组件，用 MeshRenderer',
    r'\bMeshRenderer2D\b':
        '2D 组件，勿混用',
    r'\bGameObject\.AddPrimitive\b':
        '应为 GameObject.CreatePrimitive',
    r'\bnew WaitForSeconds\s*\(\s*Time\.deltaTime\b':
        'WaitForSeconds 参数应为秒数常量，不是每帧时长',
    r'\.rotation\s*=\s*Quaternion\.LookRotation\s*\(\s*transform\.position\b':
        'LookRotation 第一个参数应是"看向的目标点"，不是自身位置',
}

# 检查「类成员是否为 public 却被外部访问」这类难以静态发现的问题，
# 这里只做最常见的一类：私有字段被其他脚本直接访问。
def check_cross_script_private(src, path, all_sources):
    problems = []
    base = os.path.splitext(os.path.basename(path))[0]
    # 收集本文件声明的 private 字段
    privates = set(re.findall(r'^\s*private\s+(?:static\s+)?(?:readonly\s+)?[\w<>\[\],\.\?]+\s+(\w+)\s*[;=]',
                               src, flags=re.M))
    if not privates:
        return problems
    for other_path, other_src in all_sources:
        if other_path == path:
            continue
        for field in privates:
            # 其他文件里出现 base.field 且不是 base 自身声明
            pattern = r'\b' + re.escape(base) + r'\.' + re.escape(field) + r'\b'
            for m in re.finditer(pattern, other_src):
                line_no = other_src[:m.start()].count('\n') + 1
                problems.append(
                    f"{os.path.relpath(other_path, ROOT)}:{line_no} 访问了 "
                    f"{base} 的 private 字段 '{field}'（应改为 internal/public 或加属性）")
    return problems


def check_file(path, all_sources):
    errors, warns = [], []
    with open(path, encoding='utf-8-sig') as f:
        raw = f.read()
    src = raw
    code = strip_code(src)
    rel = os.path.relpath(path, ROOT)

    # 1) 括号平衡
    for name, o, c in (("{}", "{", "}"), ("()", "(", ")"), ("[]", "[", "]")):
        a, b = code.count(o), code.count(c)
        if a != b:
            errors.append(f"{rel}: 括号不平衡 {name}: 开{a} 闭{b}")

    # 2) 类型声明与花括号深度匹配
    #    合法 C# 允许一个文件多个 class，所以不能简单看 '}' 后面跟什么。
    #    正确做法：按深度追踪，每个顶层类型声明必须对应一次回到深度 0 的闭合。
    lines = src.splitlines()
    type_re = re.compile(
        r'^\s*(?:\[[^\]]*\]\s*)*'                       # 可选特性
        r'(?:public|private|protected|internal)?\s*'
        r'(?:static\s+|sealed\s+|abstract\s+|partial\s+|unsafe\s+)*'
        r'(class|struct|enum|interface)\s+(\w+)')
    depth = 0
    opened_at = None          # 当前类型声明所在行
    type_count = 0
    for i, ln in enumerate(lines, 1):
        t = ln.strip()
        if t.startswith('//') or t.startswith('*') or t.startswith('/*'):
            continue
        m = type_re.match(ln)
        if m and depth == 0:
            type_count += 1
            opened_at = (i, m.group(2))
        # 计算本行深度变化（用已剔除注释/字符串的文本）
        code_ln = strip_code(ln)
        delta = code_ln.count('{') - code_ln.count('}')
        depth += delta
        if depth < 0:
            errors.append(f"{rel}:{i} 花括号深度变成负数，多了一个 '}}'")
            depth = 0
        if depth == 0 and opened_at is not None:
            opened_at = None
    if depth != 0:
        errors.append(f"{rel}: 文件结束时花括号深度为 {depth}（应为 0）")
    if type_count == 0 and re.search(r'\b(class|struct|enum|interface)\b', code):
        warns.append(f"{rel}: 找不到顶层类型声明，检查是否缩进异常")

    # 3) 常见 Unity API 误用
    for pat, msg in BAD_API.items():
        for m in re.finditer(pat, code):
            line_no = code[:m.start()].count('\n') + 1
            errors.append(f"{rel}:{line_no} {msg}")

    # 4) 分号缺失的粗检
    #    剔除行尾注释后再判断；且只对"单行完整语句"报警，
    #    跨行语句（括号未闭合）一律跳过，避免误报。
    for i, ln in enumerate(lines, 1):
        raw_t = ln.strip()
        if not raw_t or raw_t.startswith('//') or raw_t.startswith('*') or raw_t.startswith('/*'):
            continue
        if raw_t.startswith('#'):
            continue
        # 去行尾注释（不在字符串里的 //）
        t = re.sub(r'//.*$', '', raw_t).strip()
        if not t:
            continue
        if t.endswith((';', '{', '}', ')', ',', '=>', ':')):
            continue
        # 跨行语句：本行括号未闭合 → 跳过
        code_ln = strip_code(raw_t)
        if code_ln.count('(') != code_ln.count(')'):
            continue
        if code_ln.count('[') != code_ln.count(']'):
            continue
        # 跨行语句：赋值/二元运算符结尾（下一行才接完）→ 跳过
        if t.endswith(('+', '-', '*', '/', '%', '&&', '||', '?', ':', '=', '==', '!=',
                       '<', '>', '<=', '>=', '&', '|', '^')):
            continue
        # 泛型参数列表结尾 new (Vector3 n, ...)[] 这类
        if re.search(r'new\s*\([^)]*\)\s*\[\s*\]$', t):
            continue
        # 三元运算符跨行：x = cond
        #             ? a
        #             : b;      ← 本行以 ':' 开头
        if t.startswith(':'):
            continue
        # 赋值行右值以标识符结尾、下一行是 ? 或 : → 视为跨行三元
        if re.match(r'^\w+(\.\w+)*\s*=\s*\w+$', t):
            nxt = next((l.strip() for l in lines[i:] if l.strip()
                        and not l.strip().startswith('//')), '')
            if nxt.startswith('?') or nxt.startswith(':'):
                continue
        # 类型声明行
        if type_re.match(ln):
            continue
        # 明显是语句但缺分号
        if re.match(r'^(return|throw|break|continue|yield)\b', t):
            warns.append(f"{rel}:{i} 语句行尾无分号?: {t[:60]}")
            continue
        # 赋值/调用型语句（int x = 5 / Foo(); / a.b = c）缺分号
        if re.match(r'^[\w<>\[\],\.\?]+\s+\w+\s*=\s*\S', t) or \
           re.match(r'^[\w\.\(\)\[\]]+\s*\([^)]*\)\s*$', t) or \
           re.match(r'^\w+(\.\w+)+\s*=\s*\S', t):
            warns.append(f"{rel}:{i} 语句行尾无分号?: {t[:60]}")
            continue
        # 块内裸标识符（漏掉分号的赋值）
        if re.match(r'^\w+\s*=\s*\S', t):
            warns.append(f"{rel}:{i} 语句行尾无分号?: {t[:60]}")

    # 5) private 字段跨脚本访问
    errors.extend(check_cross_script_private(src, path, all_sources))

    # 6) 文件头 BOM / 编码提示（Unity 无所谓，但保持一致）
    if raw.startswith('\ufeff'):
        pass  # BOM 无害

    return errors, warns, len(lines)


def baseline_check(files, line_counts):
    """与基线对比：行数骤变（>40 行差异）视为可疑，防止整段误删。"""
    problems = []
    if not os.path.exists(BASELINE):
        return problems, None
    try:
        with open(BASELINE, encoding='utf-8') as f:
            base = json.load(f)
    except Exception:
        return problems, None

    for path, count in line_counts.items():
        rel = os.path.relpath(path, ROOT)
        if rel not in base:
            problems.append(f"{rel}: 新增文件（{count} 行）")
            continue
        old = base[rel]
        if abs(count - old) > 40:
            problems.append(
                f"{rel}: 行数从 {old} 变为 {count}（差 {count - old:+d}），"
                f"差异过大请确认是否误删")
    for rel in base:
        if rel not in line_counts:
            problems.append(f"{rel}: 文件被删除（基线 {base[rel]} 行）")
    return problems, base


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--baseline', action='store_true', help='与行数基线对比')
    ap.add_argument('--update-baseline', action='store_true', help='更新行数基线')
    args = ap.parse_args()

    files = collect()
    if not files:
        print("没有找到 .cs 文件", file=sys.stderr)
        return 1

    all_sources = []
    for p in files:
        with open(p, encoding='utf-8-sig') as f:
            all_sources.append((p, f.read()))

    total_errors = []
    total_warns = []
    line_counts = {}

    print(f"检查 {len(files)} 个脚本\n")
    for p in files:
        errors, warns, n = check_file(p, all_sources)
        line_counts[os.path.relpath(p, ROOT)] = n
        rel = os.path.relpath(p, ROOT)
        status = "OK " if not errors else "ERR"
        print(f"  [{status}] {rel}  ({n} 行)")
        for e in errors:
            print(f"         ✗ {e}")
        for w in warns:
            print(f"         ! {w}")
        total_errors.extend(errors)
        total_warns.extend(warns)

    # 基线对比
    if args.baseline:
        problems, base = baseline_check(files, line_counts)
        print()
        if base is None:
            print("  [跳过] 没有行数基线")
        elif problems:
            for pb in problems:
                print(f"  ✗ {pb}")
            total_errors.extend(problems)
        else:
            print("  [OK ] 行数基线一致")

    # 更新基线
    if args.update_baseline:
        os.makedirs(os.path.dirname(BASELINE), exist_ok=True)
        with open(BASELINE, 'w', encoding='utf-8') as f:
            json.dump(line_counts, f, indent=2, ensure_ascii=False)
        print(f"\n  已更新行数基线 → {os.path.relpath(BASELINE, ROOT)}")
        return 0 if not total_errors else 1

    print()
    if total_errors:
        print(f"✗ 发现 {len(total_errors)} 个错误")
        return 1
    if total_warns:
        print(f"! {len(total_warns)} 个警告（不阻塞）")
        return 0
    print("✓ 全部通过")
    return 0


if __name__ == '__main__':
    sys.exit(main())
