# -*- coding: utf-8 -*-
"""Считает то, что передал DupoLang."""

import sys


def main(argv):
    numbers = [float(x) for x in argv if x.strip()]

    if not numbers:
        print("Нечего считать: передайте числа аргументами.")
        return 1

    print("чисел:   ", len(numbers))
    print("сумма:   ", sum(numbers))
    print("среднее: ", sum(numbers) / len(numbers))
    print("максимум:", max(numbers))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
