#!/usr/bin/env python3
"""Имитатор устройства шоу для проверки приложения без железа.

Запускать на ДРУГОМ компьютере в той же сети: приложение отбрасывает
пакеты со своего же адреса как эхо собственного broadcast.

    python3 fake-device.py --name "Шоу 1"
    python3 fake-device.py --tcp          # подключаться по TCP-объявлению

Что делает (формат — plans/protocol.md):
  * на запрос 49 00 0D отвечает 64-байтовым пакетом 0x49 с именем в
    байтах 47–58 (Windows-1251), как ожидает DeviceResponse;
  * печатает команды 50 05 .. с позицией и интервалом от предыдущей —
    видно повтор play раз в секунду и повтор stop @ 0;
  * с --tcp на объявление 4C 06 подключается к адресу и порту из пакета.

Только стандартная библиотека Python 3.
"""
import argparse
import socket
import struct
import sys
import time

PORT = 333
COMMANDS = {2: "play", 3: "pause", 4: "stop", 5: "next", 6: "prev"}


def make_response(name: str) -> bytes:
    data = bytearray(64)
    data[0] = 0x49
    data[1] = 0x01  # произвольные байты: формат остального неизвестен
    encoded = name.encode("cp1251")[:12]
    data[47:47 + len(encoded)] = encoded
    data[63] = 0x0D
    return bytes(data)


def describe_playback(data: bytes) -> str:
    command = COMMANDS.get(data[2], f"0x{data[2]:02X}")
    position = struct.unpack_from("<I", data, 3)[0]
    ok = "" if data[7] == 0x0D else "  !терминатор"
    return f"{command:5} @ {position} мс{ok}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--name", default="FakeDevice", help="имя в ответе, до 12 байт cp1251")
    parser.add_argument("--port", type=int, default=PORT)
    parser.add_argument("--tcp", action="store_true", help="подключаться по TCP-объявлению")
    parser.add_argument("--silent", action="store_true", help="не отвечать на запрос обнаружения")
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    if hasattr(socket, "SO_REUSEPORT"):
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEPORT, 1)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
    sock.bind(("0.0.0.0", args.port))

    response = make_response(args.name)
    connections = []
    last = {}
    print(f"Слушаю UDP {args.port}, имя «{args.name}». Ctrl-C — выход.", flush=True)

    while True:
        data, (host, port) = sock.recvfrom(2048)
        now = time.monotonic()
        stamp = time.strftime("%H:%M:%S")
        hex_data = data.hex(" ").upper()

        if data[:2] == b"\x50\x05" and len(data) >= 8:
            key = (host, data[2])
            interval = f"  +{now - last[key]:.2f} с" if key in last else ""
            last[key] = now
            print(f"[{stamp}] {host}:{port}  {describe_playback(data)}{interval}", flush=True)
        elif data == b"\x49\x00\x0D":
            print(f"[{stamp}] {host}:{port}  запрос обнаружения", flush=True)
            if not args.silent:
                sock.sendto(response, (host, port))
                print(f"           → ответ {len(response)} байт на {host}:{port}", flush=True)
        elif data[:2] == b"\x4C\x06" and len(data) >= 9:
            address = ".".join(str(b) for b in data[2:6])
            tcp_port = struct.unpack_from("<H", data, 6)[0]
            print(f"[{stamp}] {host}:{port}  TCP-объявление: {address}:{tcp_port}", flush=True)
            if args.tcp:
                try:
                    conn = socket.create_connection((address, tcp_port), timeout=3)
                    connections.append(conn)
                    print(f"           → подключился с порта {conn.getsockname()[1]}", flush=True)
                except OSError as error:
                    print(f"           ! не подключился: {error}", flush=True)
        elif data[:1] == b"\x49":
            pass  # свой ответ, вернувшийся broadcast'ом
        else:
            print(f"[{stamp}] {host}:{port}  неизвестно: {hex_data}", flush=True)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(0)
