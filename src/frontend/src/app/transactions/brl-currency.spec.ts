import { describe, expect, it } from 'vitest';
import { brlInputFromInvariant, formatBrlInput, invariantFromBrlInput } from './brl-currency';

describe('BRL currency input', () => {
  it.each([
    ['', ''],
    ['1', 'R$ 0,01'],
    ['12', 'R$ 0,12'],
    ['123', 'R$ 1,23'],
    ['123456', 'R$ 1.234,56'],
    ['R$ 1.234,56', 'R$ 1.234,56'],
    ['abc123456', 'R$ 1.234,56'],
    ['R$ -1a2.3,4x', 'R$ 12,34'],
  ])('formats %s as %s', (input, expected) => {
    expect(formatBrlInput(input)).toBe(expected);
  });

  it('converts without floating point arithmetic', () => {
    expect(invariantFromBrlInput('R$ 1.234,56')).toBe('1234.56');
    expect(brlInputFromInvariant('1234.56')).toBe('R$ 1.234,56');
  });

  it('rejects zero, malformed and values above the domain limit', () => {
    expect(invariantFromBrlInput('R$ 0,00')).toBeNull();
    expect(invariantFromBrlInput('texto')).toBeNull();
    expect(formatBrlInput('99999999999999999999')).toBeNull();
  });

  it('keeps only digits from typed or pasted input', () => {
    expect(formatBrlInput('a1b2c3')).toBe('R$ 1,23');
    expect(formatBrlInput('R$ -1.234,56')).toBe('R$ 1.234,56');
  });

  it('accepts the exact maximum and supports removal back to an empty value', () => {
    expect(formatBrlInput('9999999999999999999')).toBe('R$ 99.999.999.999.999.999,99');
    expect(invariantFromBrlInput('R$ 99.999.999.999.999.999,99')).toBe('99999999999999999.99');
    expect(formatBrlInput('')).toBe('');
  });
});
