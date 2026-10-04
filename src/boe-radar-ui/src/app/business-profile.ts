export interface BusinessProfile {
  businessType: string;
  activity: string;
  territory: string;
}

export const businessTypes = [
  { value: 'autonomous', label: 'Autónomo/a' },
  { value: 'sme', label: 'Pyme' },
];
export const activityOptions = [
  { value: 'other', label: 'Otra actividad / varias actividades' },
  { value: 'retail', label: 'Comercio y venta al público' },
  { value: 'hospitality', label: 'Hostelería y turismo' },
  { value: 'construction', label: 'Construcción y rehabilitación' },
  { value: 'technology', label: 'Tecnología e innovación' },
  { value: 'professional', label: 'Servicios profesionales' },
  { value: 'agriculture', label: 'Agricultura, ganadería y pesca' },
  { value: 'transport', label: 'Transporte y movilidad' },
  { value: 'educationSport', label: 'Formación y deporte' },
];
export const territoryOptions = [
  { value: 'all', label: 'Varios territorios / sin especificar' },
  { value: 'andalucia', label: 'Andalucía' }, { value: 'aragon', label: 'Aragón' },
  { value: 'asturias', label: 'Asturias' }, { value: 'baleares', label: 'Illes Balears' },
  { value: 'canarias', label: 'Canarias' }, { value: 'cantabria', label: 'Cantabria' },
  { value: 'castillaLaMancha', label: 'Castilla-La Mancha' },
  { value: 'castillaLeon', label: 'Castilla y León' }, { value: 'cataluna', label: 'Cataluña' },
  { value: 'valencia', label: 'Comunitat Valenciana' }, { value: 'extremadura', label: 'Extremadura' },
  { value: 'galicia', label: 'Galicia' }, { value: 'madrid', label: 'Comunidad de Madrid' },
  { value: 'murcia', label: 'Región de Murcia' }, { value: 'navarra', label: 'Navarra' },
  { value: 'paisVasco', label: 'País Vasco' }, { value: 'laRioja', label: 'La Rioja' },
  { value: 'ceuta', label: 'Ceuta' }, { value: 'melilla', label: 'Melilla' },
];

const storageKey = 'boe-radar-business-profile-v1';
export function isBusinessProfile(value: unknown): value is BusinessProfile {
  if (!value || typeof value !== 'object') return false;
  const profile = value as BusinessProfile;
  return businessTypes.some(option => option.value === profile.businessType) &&
    activityOptions.some(option => option.value === profile.activity) &&
    territoryOptions.some(option => option.value === profile.territory);
}

export function loadBusinessProfile(): BusinessProfile | null {
  try {
    const envelope = JSON.parse(localStorage.getItem(storageKey) ?? 'null');
    return envelope?.version === 1 && isBusinessProfile(envelope.profile)
      ? { businessType: envelope.profile.businessType, activity: envelope.profile.activity,
          territory: envelope.profile.territory } : null;
  } catch { return null; }
}

export function storeBusinessProfile(profile: BusinessProfile | null): boolean {
  try {
    if (profile) localStorage.setItem(storageKey, JSON.stringify({ version: 1, profile }));
    else localStorage.removeItem(storageKey);
    return true;
  } catch { return false; }
}
