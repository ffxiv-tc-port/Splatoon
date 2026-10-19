#!/usr/bin/env python3
"""
強制「改了腳本就要 bump Metadata 版本」。

為什麼需要這個閘門
------------------
Splatoon 的腳本更新器只做一件事:比對 update.csv 裡的版本數字有沒有比本機大。
它不檢查內容雜湊、不檢查 API 世代、也不檢查修改時間。所以只要版本號沒動,
我們對腳本做的任何修正(包含台服專屬的記憶體位移修正)都永遠不會送到使用者手上 ——
而且完全沒有徵兆:CI 全綠、檔案確實改了、update.csv 也確實更新了,
使用者本機那份就是不會動。

這正是接管腳本策展之後最容易重演的坑:我們把來源改成自己的 repo,
卻沿用上游「改腳本不 bump 版本」的習慣,等於把更新鏈接好了卻不通電。

做法
----
用「同一支產生器」分別解析 base 與 head 兩側的腳本,再比對版本。
刻意不自己重寫一份版本擷取正規表示式 —— 用 ScriptUpdateFileGenerator 本人來解析,
比對的就是「實際會寫進 update.csv 的那個數字」,不會有解析器漂移。

逃生口
------
commit 訊息含 [skip script version check] 即整批略過。
給大規模機械式改動用(例如全艦隊 API 遷移、格式化),
那種情況下逼 241 支腳本全部 bump 只會讓所有使用者重下載一次,沒有意義。
"""

import os
import subprocess
import sys
import tempfile

SCRIPTS_DIR = "SplatoonScripts"
GENERATOR = "ScriptUpdateFileGenerator/ScriptUpdateFileGenerator.csproj"
SKIP_MARKER = "[skip script version check]"
ZERO_SHA = "0" * 40


def run(cmd, **kw):
    # 一定要指定 encoding。text=True 會用「執行環境的地區設定」解碼,
    # 而我們的 commit 訊息一律是中文 —— 在非 UTF-8 的地區設定下(例如本機 Windows 的 cp950)
    # 讀 git log 會直接 UnicodeDecodeError 炸掉。不要相信 runner 的預設編碼。
    out = subprocess.run(cmd, check=True, capture_output=True,
                         encoding="utf-8", errors="replace", **kw).stdout
    return out or ""


def skip(reason):
    print(f"跳過腳本版本檢查{os.linesep}  原因 {reason}")
    sys.exit(0)


def parse_versions(csv_path):
    """讀產生器輸出,回傳 {腳本相對路徑: 版本}。

    第三欄是完整下載網址,結尾就是腳本在 SplatoonScripts/ 底下的相對路徑,
    所以用 '/SplatoonScripts' 切開即可取得 key,不必知道網址前綴是什麼 ——
    這樣之後換 repo/換分支都不會弄壞這支檢查。
    """
    result = {}
    if not os.path.exists(csv_path):
        return result
    with open(csv_path, encoding="utf-8") as fh:
        for line in fh.read().split("\n"):
            parts = line.split(",", 2)
            if len(parts) < 3:
                continue
            try:
                version = int(parts[1])
            except ValueError:
                continue
            marker = "/" + SCRIPTS_DIR
            if marker not in parts[2]:
                continue
            result[parts[2].split(marker, 1)[1]] = version
    return result


def generate(source_dir, out_csv):
    subprocess.run(
        ["dotnet", "run", "--project", GENERATOR, source_dir, out_csv],
        check=True, capture_output=True, encoding="utf-8", errors="replace",
    )
    return parse_versions(out_csv)


def main():
    if os.environ.get("EVENT_NAME") != "push":
        skip("非 push 事件,沒有可靠的 base 可以比對")

    base = os.environ.get("BEFORE_SHA", "")
    if not base or base == ZERO_SHA:
        skip("這是新分支的第一次推送,沒有前一個狀態")

    if subprocess.run(["git", "cat-file", "-e", base + "^{commit}"],
                      capture_output=True).returncode != 0:
        skip(f"base commit {base} 不存在(強制推送或歷史重寫),無從比對")

    messages = run(["git", "log", "--format=%B", f"{base}..HEAD"])
    if SKIP_MARKER in messages:
        skip(f"commit 訊息含有 {SKIP_MARKER}")

    changed = [
        p for p in run([
            "git", "diff", "--name-only", "--no-renames",
            "--diff-filter=M", base, "HEAD", "--", SCRIPTS_DIR,
        ]).split("\n")
        if p.strip().endswith(".cs")
    ]
    if not changed:
        skip("這次推送沒有修改任何既有腳本檔")

    print(f"本次修改了 {len(changed)} 支既有腳本,開始比對版本")

    with tempfile.TemporaryDirectory() as tmp:
        # 只把「有改動的腳本」的 base 版本還原到一棵最小的樹,
        # 相對路徑保持一致,產生器算出來的 key 就會和 head 側對得起來。
        base_tree = os.path.join(tmp, "base", SCRIPTS_DIR)
        for path in changed:
            rel = path[len(SCRIPTS_DIR):].lstrip("/")
            dest = os.path.join(base_tree, rel)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            blob = subprocess.run(["git", "show", f"{base}:{path}"],
                                  check=True, capture_output=True)
            with open(dest, "wb") as fh:
                fh.write(blob.stdout)

        base_versions = generate(base_tree, os.path.join(tmp, "base.csv"))
        head_versions = generate(SCRIPTS_DIR, os.path.join(tmp, "head.csv"))

    failures, skipped = [], []
    for path in changed:
        key = path[len(SCRIPTS_DIR):]
        old, new = base_versions.get(key), head_versions.get(key)
        if old is None or new is None:
            # 解析不到版本代表這個檔案根本進不了 update.csv,也就永遠不會被派送。
            skipped.append(path)
            continue
        if new > old:
            print(f"  OK   {path}  v{old} -> v{new}")
        else:
            failures.append((path, old, new))

    for path in skipped:
        print(f"  警告 {path} 解析不到 Metadata 版本,不會出現在 update.csv,無法派送更新")

    if failures:
        print("")
        print("腳本版本檢查失敗 —— 以下腳本內容有改動但版本沒有提高")
        for path, old, new in failures:
            print(f"  {path}  版本仍是 v{new}(base 是 v{old})")
        print("")
        print("更新器只認版本數字。版本沒動 = 使用者本機那份永遠不會被替換,")
        print("修正等於沒發出去,而且不會有任何錯誤訊息。")
        print(f"請提高 Metadata 的版本號,或在 commit 訊息加上 {SKIP_MARKER} 略過。")
        sys.exit(1)

    print("腳本版本檢查通過")


if __name__ == "__main__":
    main()
