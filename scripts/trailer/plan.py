"""EQBuddy Evolved launch trailer - the edit decision list.

    python scripts/trailer/plan.py <build-dir>

Writes <build-dir>/plan.json, which trailer.html reads. Every rectangle below is measured in
the pixels of the capture it belongs to (shoot.ps1's 'trailer-*' stills are 1226x813, 31 px
of native title bar on top, which every crop drops). Times are seconds on the score's clock:
96 BPM, so a bar is 2.5 s and the cuts sit on bar lines.

    0 - 5     cold open            the question
    5 - 10    logo                 impact
    10 - 25   in game              the live HUD take over the blurred game
    25 - 32.5 Helper: hunt
    32.5 - 40 Helper: gear
    40 - 47.5 Guide: Plane of Sky
    47.5-52.5 World drops + phone
    52.5-57.5 principles
    57.5 -    end card
"""
import json, sys
from pathlib import Path

build = Path(sys.argv[1])

plan = {
    'duration': 65.5,
    'game': {'dir': 'game', 'pattern': '%05d.jpg', 'fps': 30, 'frames': 510, 'start': 0.0, 'rate': 1.0},
    # hud/%05d.png: frame i is the take at (i-1)/30 s. Beat 0 opens just before the loot
    # peek and runs through the damage peek; beat 1 is the alert toast, the spawn row and
    # the kill at ~18 s that moves the coin.
    'hud': {'dir': 'hud', 'pattern': '%05d.png', 'fps': 30, 'frames': 600,
            'beats': [{'from': 1.4}, {'from': 13.5}],
            'cam': [
                [{'t': 10, 'x': 0, 'y': 0, 's': 1.0}, {'t': 20, 'x': -38, 'y': -8, 's': 1.04}],
                [{'t': 20, 'x': 0, 'y': 0, 's': 1.0}, {'t': 25, 'x': -77, 'y': -43, 's': 1.08}],
            ]},
    'phone': {'src': 'phone.png', 'a': 47.8, 'b': 52.6, 'x': 1030, 'y': 130, 'scroll': 700},
    'captions': {
        'q1': {'head': 'Every session starts with the same question.', 'size': 46,
               'x': 0, 'y': 470, 'w': 1920, 'align': 'center', 'a': 0.45, 'b': 2.55},
        'q2': {'head': 'What should I do <em>next?</em>', 'size': 96,
               'x': 0, 'y': 440, 'w': 1920, 'align': 'center', 'a': 2.65, 'b': 4.95},
        'hud1': {'kicker': 'While you play', 'head': 'One thin bar.<br>Only what you chose.',
                 'x': 110, 'y': 800, 'a': 10.5, 'b': 13.6},
        'hud2': {'kicker': 'Hover to peek', 'head': 'Every number<br>opens up.',
                 'x': 110, 'y': 800, 'a': 13.8, 'b': 19.7},
        'hud3': {'kicker': 'Alerts &amp; timers', 'head': 'Know what’s coming.',
                 'sub': 'Watch alerts, and spawn timers learned from your own kills.', 'subW': 620,
                 'x': 110, 'y': 800, 'a': 20.3, 'b': 25.0},
        'hunt': {'kicker': 'Helper', 'head': 'Where should<br>I hunt?',
                 'sub': 'Ranked from your own kills, XP and time — not a guide written for someone else.',
                 'subW': 560, 'x': 110, 'y': 360, 'a': 25.2, 'b': 32.3},
        'gear': {'kicker': 'Gear', 'head': 'How do I<br>upgrade this?',
                 'sub': 'Starts from what you’re wearing, and names who drops something better.',
                 'subW': 560, 'x': 110, 'y': 360, 'a': 32.7, 'b': 39.8},
        'sky': {'kicker': 'Guide', 'head': 'What’s next in<br>Plane of Sky?',
                'sub': 'Your turn-ins, your classes, your bags — read from the files the game writes for you.',
                'subW': 560, 'x': 110, 'y': 360, 'a': 40.2, 'b': 47.3},
        'mobile': {'kicker': 'EQBuddy Mobile', 'head': 'On the phone<br>you look away to.',
                   'sub': 'Served by your PC over your own Wi-Fi. No account.', 'subW': 400,
                   'x': 1480, 'y': 380, 'w': 420, 'a': 47.8, 'b': 52.4},
    },
    'rooms': {
        'hunt': {'src': 'rooms/trailer-helper-hunt.png', 'crop': [8, 31, 744, 350],
                 'a': 25.0, 'b': 32.5,
                 'cam': [{'t': 25, 'x': 780, 'y': 310, 's': 1.36, 'ry': -5},
                         {'t': 32.5, 'x': 740, 'y': 290, 's': 1.42, 'ry': -2}],
                 'spots': [{'r': [214, 238, 530, 140], 'a': 26.6, 'b': 32.2, 'tag': 'Your own numbers', 'tagBelow': True}]},
        'gear': {'src': 'rooms/trailer-helper-gear.png', 'crop': [8, 31, 744, 640],
                 'a': 32.5, 'b': 40.0,
                 'cam': [{'t': 32.5, 'x': 820, 'y': 150, 's': 1.2, 'ry': -5},
                         {'t': 40, 'x': 800, 'y': 40, 's': 1.26, 'ry': -2}],
                 'spots': [{'r': [214, 441, 530, 86], 'a': 34.3, 'b': 39.7, 'tag': 'From what you wear', 'tagBelow': True}]},
        'sky': {'src': 'rooms/trailer-quests-sky.png', 'crop': [210, 75, 1000, 760],
                'a': 40.0, 'b': 47.5,
                'cam': [{'t': 40, 'x': 800, 'y': 150, 's': 1.0, 'ry': -5},
                        {'t': 47.5, 'x': 770, 'y': 120, 's': 1.06, 'ry': -2}],
                'spots': [{'r': [386, 102, 132, 28], 'a': 41.4, 'b': 43.9, 'tag': 'Your progress', 'tagBelow': True},
                          {'r': [220, 377, 978, 148], 'a': 44.1, 'b': 47.1, 'tag': 'Already in your bags', 'tagBelow': True}]},
        'world': {'src': 'rooms/trailer-world-drops.png', 'crop': [210, 75, 560, 700],
                  'a': 47.5, 'b': 52.6,
                  'cam': [{'t': 47.5, 'x': 150, 'y': 190, 's': 1.2, 'ry': 4},
                          {'t': 52.6, 'x': 150, 'y': 170, 's': 1.24, 'ry': 2}],
                  'spots': [{'r': [224, 471, 240, 22], 'a': 48.9, 'b': 52.3, 'tag': 'Your own drop rates', 'tagBelow': True}]},
    },
}
(build / 'plan.json').write_text(json.dumps(plan, indent=1), encoding='utf-8')
print('wrote', build / 'plan.json')
