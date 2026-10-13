"""Tiny client for the Blender MCP addon socket (localhost:9876): sends one command, prints the JSON reply.
Usage: python3 bmcp.py exec <file.py>      run a Python file inside the live Blender session
       python3 bmcp.py run <file.py>       same, inside a window/area context override (needed by bpy.ops primitives)
       python3 bmcp.py info                 scene info
"""
import json, socket, sys


def call(cmd_type, params=None, timeout=600):
    s = socket.create_connection(('localhost', 9876), timeout=timeout)
    s.sendall(json.dumps({'type': cmd_type, 'params': params or {}}).encode())
    buf = b''
    while True:
        chunk = s.recv(1 << 20)
        if not chunk:
            break
        buf += chunk
        try:
            return json.loads(buf.decode())
        except ValueError:
            continue
    return json.loads(buf.decode())


if __name__ == '__main__':
    if sys.argv[1] == 'exec':
        code = open(sys.argv[2]).read()
        r = call('execute_code', {'code': code})
    elif sys.argv[1] == 'run':
        path = __import__('os').path.abspath(sys.argv[2])
        code = ("import bpy\nwin = bpy.context.window_manager.windows[0]\n"
                "area = max(win.screen.areas, key=lambda a: a.width * a.height)\n"
                "with bpy.context.temp_override(window=win, screen=win.screen, area=area):\n"
                f"    exec(compile(open({path!r}).read(), {path!r}, 'exec'), {{'__name__': '__main__'}})\n")
        r = call('execute_code', {'code': code})
        out = r.get('result', {}).get('result', '') if r.get('status') == 'success' else json.dumps(r)
        print('\n'.join(l for l in out.splitlines() if 'INFO:' not in l)[-6000:]); sys.exit(0)
    else:
        r = call('get_scene_info')
    print(json.dumps(r, ensure_ascii=False)[:20000])
