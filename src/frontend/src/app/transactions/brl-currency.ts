const MAX_DIGITS = 19;

function digitsFrom(value: string): string {
  return value.replace(/\D/g, '').replace(/^0+(?=\d)/, '');
}

export function formatBrlInput(value: string): string | null {
  if (!value) return '';
  const digits = digitsFrom(value);
  if (!digits) return value.trim() ? null : '';
  if (digits.length > MAX_DIGITS) return null;
  const padded = digits.padStart(3, '0');
  const whole = padded.slice(0, -2).replace(/^0+(?=\d)/, '');
  const cents = padded.slice(-2);
  return `R$ ${whole.replace(/\B(?=(\d{3})+(?!\d))/g, '.')},${cents}`;
}

export function invariantFromBrlInput(value: string): string | null {
  const formatted = formatBrlInput(value);
  if (!formatted) return null;
  const digits = digitsFrom(formatted);
  if (!digits || /^0+$/.test(digits)) return null;
  const padded = digits.padStart(3, '0');
  return `${padded.slice(0, -2).replace(/^0+(?=\d)/, '')}.${padded.slice(-2)}`;
}

export function brlInputFromInvariant(value: string): string {
  const match = /^(\d{1,17})(?:\.(\d{1,2}))?$/.exec(value);
  if (!match) return '';
  return formatBrlInput(`${match[1]}${(match[2] ?? '').padEnd(2, '0')}`) ?? '';
}
