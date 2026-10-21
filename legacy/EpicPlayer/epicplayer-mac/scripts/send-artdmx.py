#!/usr/bin/env python3
"""Отправка ArtDmx для проверки реакции на DMX без пульта.

    python3 send-artdmx.py 192.168.88.255 --channel 1 --values 50 0 100 0 150
    python3 send-artdmx.py 192.168.88.255 --channel 1 --values 50 --hold 3

Значения по очереди, каждое удерживается --hold секунд и шлётся с частотой
--rate пакетов в секунду, как делает пульт. Удержание проверяет, что
команда не повторяется: приложение реагирует только на изменение.

--channel — номер канала DMX с единицы, как в настройках macOS-версии.
"""
import argparse
import socket
import struct
import time


def art_dmx(universe: int, channel: int, value: int, sequence: int) -> bytes:
    data = bytearray(512)
    data[channel - 1] = value
    header = b"Art-Net\x00" + struct.pack("<H", 0x5000) + bytes([0, 14])
    header += bytes([sequence, 0, universe & 0xFF, (universe >> 8) & 0x7F])
    header += struct.pack(">H", len(data))
    return header + bytes(data)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("target", help="адрес: broadcast подсети или IP Mac")
    parser.add_argument("--universe", type=int, default=0)
    parser.add_argument("--channel", type=int, default=1, help="1–512")
    parser.add_argument("--values", type=int, nargs="+", default=[50, 0, 100, 0, 150, 0])
    parser.add_argument("--hold", type=float, default=2.0, help="секунд на значение")
    parser.add_argument("--rate", type=float, default=30.0, help="пакетов в секунду")
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)

    sequence = 1
    for value in args.values:
        print(f"universe {args.universe}, канал {args.channel} = {value} ({args.hold} с)", flush=True)
        end = time.monotonic() + args.hold
        while time.monotonic() < end:
            sock.sendto(art_dmx(args.universe, args.channel, value, sequence), (args.target, 6454))
            sequence = sequence % 255 + 1
            time.sleep(1 / args.rate)


if __name__ == "__main__":
    main()
