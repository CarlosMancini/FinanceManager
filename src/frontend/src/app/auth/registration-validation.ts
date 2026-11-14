export interface RegistrationData { fullName: string; nickname: string; birthDate: string; email: string; phoneNumber: string; password: string; }

export function passwordCriteria(password: string) {
  const chars = Array.from(password);
  return { length: chars.length >= 12 && chars.length <= 128, uppercase: /\p{Lu}/u.test(password), lowercase: /\p{Ll}/u.test(password), digit: /[0-9]/.test(password), symbol: /[^\p{L}\p{N}\s]/u.test(password) };
}

export function validateRegistration(data: RegistrationData, today: string): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!data.fullName.trim()) errors['fullName'] = 'Informe o nome completo.';
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(data.birthDate);
  const date = match ? new Date(0) : null;
  if (date && match) { date.setUTCHours(0, 0, 0, 0); date.setUTCFullYear(Number(match[1]), Number(match[2]) - 1, Number(match[3])); }
  if (!match || !date || date.toISOString().slice(0, 10) !== data.birthDate || data.birthDate > today) errors['birthDate'] = 'Informe uma data de nascimento válida.';
  if (!/^[^@\s]+@[^@\s]+$/u.test(data.email.trim())) errors['email'] = 'Informe um e-mail válido.';
  const phone = data.phoneNumber.trim();
  const digits = phone.replace(/[ ()+\-]/g, '');
  if (!/^\+?[0-9 ()-]+$/.test(phone) || digits.length < 10 || digits.length > 15) errors['phoneNumber'] = 'Informe um telefone válido.';
  if (!Object.values(passwordCriteria(data.password)).every(Boolean)) errors['password'] = 'Confira os critérios da senha.';
  return errors;
}

export function saoPauloToday(): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
  const value = (type: string) => parts.find(part => part.type === type)?.value ?? '';
  return `${value('year')}-${value('month')}-${value('day')}`;
}
