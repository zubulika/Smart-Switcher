"""
Smart Switcher Development Supervisor (dev.py)
Watches code, UI, and asset files; automatically builds and relaunches SmartSwitcher on changes.
"""

import os
import sys
import time
import subprocess
import signal

ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
SRC_DIR = os.path.join(ROOT_DIR, "src", "SmartSwitcher")
PROJECT_FILE = os.path.join(SRC_DIR, "SmartSwitcher.csproj")
EXE_PATH = os.path.join(SRC_DIR, "bin", "Debug", "net8.0-windows", "SmartSwitcher.exe")

WATCH_EXTENSIONS = {".cs", ".csproj", ".manifest", ".svg", ".png", ".ttf", ".ico", ".json"}
IGNORE_DIRS = {"bin", "obj", ".git", ".vs", "logs"}

def get_watched_files():
    """Recursively collect watched files and their modification timestamps."""
    files_snapshot = {}
    for root, dirs, files in os.walk(SRC_DIR):
        dirs[:] = [d for d in dirs if d not in IGNORE_DIRS]
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext in WATCH_EXTENSIONS:
                full_path = os.path.join(root, f)
                try:
                    files_snapshot[full_path] = os.path.getmtime(full_path)
                except OSError:
                    pass
    return files_snapshot

def kill_running_app():
    """Force-terminate any running SmartSwitcher instances and wait for file locks to release."""
    try:
        # 1. Try standard taskkill first
        result = subprocess.run(
            ["taskkill", "/F", "/IM", "SmartSwitcher.exe", "/T"],
            capture_output=True,
            text=True
        )
        # 2. If access denied (e.g. process is elevated but Python is not), elevate taskkill
        if result.returncode != 0 and "Access is denied" in (result.stderr or result.stdout):
            elevated_cmd = "Start-Process taskkill -ArgumentList '/F /IM SmartSwitcher.exe /T' -Verb RunAs -WindowStyle Hidden -Wait"
            subprocess.run(["powershell", "-NoProfile", "-Command", elevated_cmd], capture_output=True)

        # 3. Wait up to 3 seconds for all instances to exit and file locks to release
        for _ in range(30):
            check = subprocess.run(
                ["powershell", "-NoProfile", "-Command", "Get-Process -Name 'SmartSwitcher' -ErrorAction SilentlyContinue"],
                capture_output=True,
                text=True
            )
            if not check.stdout.strip():
                break
            time.sleep(0.1)

        # Give Windows a brief moment to release file system handles
        time.sleep(0.3)
    except Exception as e:
        print(f"\033[93m[dev] Note during process shutdown: {e}\033[0m")

def run_build():
    """Build the solution with dotnet."""
    print("\n\033[94m[dev] 🔨 Building Smart Switcher...\033[0m")
    t0 = time.time()
    result = subprocess.run(
        ["dotnet", "build", PROJECT_FILE, "-c", "Debug", "--nologo", "-v", "q"],
        cwd=ROOT_DIR,
        capture_output=True,
        text=True
    )
    elapsed = time.time() - t0
    if result.returncode == 0:
        print(f"\033[92m[dev] ✓ Build succeeded in {elapsed:.2f}s\033[0m")
        return True
    else:
        print(f"\033[91m[dev] ✗ Build failed ({elapsed:.2f}s):\033[0m")
        print(result.stdout)
        print(result.stderr)
        return False

def launch_app():
    """Launch SmartSwitcher.exe with administrator privileges."""
    print("\033[96m[dev] 🚀 Relaunching SmartSwitcher (Elevated)...\033[0m")
    ps_cmd = f"Start-Process '{EXE_PATH}' -Verb RunAs"
    try:
        subprocess.Popen(
            ["powershell", "-NoProfile", "-Command", ps_cmd],
            cwd=ROOT_DIR,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL
        )
    except Exception as e:
        print(f"\033[91m[dev] Failed to launch: {e}\033[0m")

def main():
    print("=" * 60)
    print("\033[95m  Smart Switcher - Live Dev Supervisor (Auto-Reload)\033[0m")
    print(f"  Watching: {SRC_DIR}")
    print("  Press Ctrl+C to stop.")
    print("=" * 60)

    # Initial build & launch
    kill_running_app()
    if run_build():
        launch_app()

    last_snapshot = get_watched_files()
    print(f"\033[92m[dev] 👁️  Supervisor active. Watching {len(last_snapshot)} files. Ready for changes...\033[0m\n")

    try:
        while True:
            time.sleep(0.5)
            current_snapshot = get_watched_files()

            # Detect modifications or additions
            changed_files = []
            for path, mtime in current_snapshot.items():
                if path not in last_snapshot or mtime > last_snapshot[path]:
                    changed_files.append(path)

            # Detect deletions
            for path in last_snapshot:
                if path not in current_snapshot:
                    changed_files.append(path)

            if changed_files:
                # Debounce rapid file writes
                time.sleep(0.3)
                last_snapshot = get_watched_files()

                rel_names = [os.path.relpath(f, ROOT_DIR) for f in changed_files[:3]]
                summary = ", ".join(rel_names)
                if len(changed_files) > 3:
                    summary += f" and {len(changed_files) - 3} more"

                print(f"\n\033[93m[dev] ⚡ Change detected in: {summary}\033[0m")
                kill_running_app()
                time.sleep(0.2)
                if run_build():
                    launch_app()

    except KeyboardInterrupt:
        print("\n\033[93m[dev] Stopping Smart Switcher and exiting...\033[0m")
        kill_running_app()
        print("\033[92m[dev] Goodbye!\033[0m")
        sys.exit(0)

if __name__ == "__main__":
    main()
