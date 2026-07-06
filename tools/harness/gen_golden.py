#!/usr/bin/env python3
"""Generate golden byte-parity fixtures from the reverse-engineered reference
builders (faithful to the main.py server stub). These are the ground-truth wire
bytes the C# builders must reproduce exactly.

Deterministic: the login-return's player-id field (which the stub randomizes) is
passed in as a fixed value here so the output is stable.

Usage:
    python3 tools/harness/gen_golden.py > tests/FOM.Server.Tests/Golden/golden.json
"""
import json
import struct


def put_u8(b, v):  b += struct.pack(">B", v)
def put_u16(b, v): b += struct.pack(">H", v)
def put_i16(b, v): b += struct.pack(">h", v)
def put_u32(b, v): b += struct.pack(">I", v)
def put_u32_le(b, v): b += struct.pack("<I", v)


def put_fixed_cstring(b, s, n):
    raw = s.encode()[:n]
    b += raw + b"\0" * (n - len(raw))


def pack_appearance(rank, faction, female, leg, arm, torso, head, model):
    code = (
        ((rank & 0xF) << 28)
        | ((faction & 0xF) << 24)
        | ((int(female) & 1) << 23)
        | ((leg & 0xF) << 19)
        | ((arm & 0xF) << 15)
        | ((torso & 0xF) << 11)
        | ((head & 0x3F) << 5)
        | (model & 0x1F)
    )
    return code & 0xFFFFFFFF


def frame(opcode, body):
    return struct.pack(">HH", opcode, len(body)) + bytes(body)


def enter_world(status, world, node):
    b = bytearray()
    put_u32(b, status); put_u32(b, world); put_u16(b, node); put_u16(b, 0)
    return frame(0x03EB, b)


def pong(ts):
    b = bytearray(); put_u32(b, ts)
    return frame(0x07E5, b)


def build_chat(sender_id, channel, name, msg):
    b = bytearray()
    put_u32(b, sender_id); put_u16(b, channel); put_u16(b, len(msg))
    put_fixed_cstring(b, name, 28)
    b += msg.encode("latin1", "replace")[:250] + b"\0"
    return frame(0x03EA, b)


def login_return(header_id, status, hp, stam, psi, conc, uc, xp, bdgt, pp,
                 appearance, player_id, world, apt_tier, name, tag, desc):
    p = bytearray()
    put_u32(p, header_id); put_u16(p, status); put_u16(p, 0xFFFF)
    put_i16(p, hp); put_i16(p, stam); put_i16(p, psi); put_i16(p, conc)
    put_u32(p, uc); put_u32(p, xp); put_u32(p, bdgt); put_u32_le(p, pp)
    put_u32(p, appearance); put_u32(p, player_id)
    put_u8(p, world); put_u8(p, apt_tier); put_u16(p, 0)
    for _ in range(10):
        put_u32(p, 0)
    put_u16(p, 0); put_i16(p, 0); put_u16(p, 1)
    put_fixed_cstring(p, name, 20); put_fixed_cstring(p, tag, 4); put_fixed_cstring(p, desc, 506)
    return frame(0x07D2, p)


APPEARANCE = pack_appearance(7, 1, False, 1, 1, 1, 1, 0)  # 0x71088820

fixtures = {
    "pong": pong(0x12345678),
    "enter_world": enter_world(4, 21, 1),  # status 4, StsGenesis, node 1
    "chat": build_chat(42, 3, "Neo", "hi"),
    "login_return": login_return(
        12345, 6, 100, 100, 100, 100, 1000, 100, 0, 10,
        APPEARANCE, 1001, 21, 1, "Neo", "", ""),
    "appearance": struct.pack(">I", APPEARANCE),  # raw u32, not framed
}

print(json.dumps({name: bytes(data).hex().upper() for name, data in fixtures.items()}, indent=2))
