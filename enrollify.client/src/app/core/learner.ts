import { EnrollmentLearnerInfo, LearnerProfile, PsgcAddress } from './models';

export function emptyPsgcAddress(): PsgcAddress {
  return {
    regionCode: null, regionName: null, provinceCode: null, provinceName: null,
    cityMunicipalityCode: null, cityMunicipalityName: null, barangayCode: null, barangayName: null, zipCode: null,
  };
}

export function emptyLearnerProfile(): LearnerProfile {
  return {
    extensionName: null, psaBirthCertificateNumber: null, placeOfBirth: null, motherTongue: null, religion: null,
    isIndigenousPeople: false, ipCommunity: null, is4PsBeneficiary: false, fourPsHouseholdId: null,
    hasDisability: false, disabilityTypes: null,
    fatherLastName: null, fatherFirstName: null, fatherMiddleName: null, fatherContactNumber: null,
    motherMaidenLastName: null, motherMaidenFirstName: null, motherMaidenMiddleName: null, motherContactNumber: null,
    homeAddress: emptyPsgcAddress(),
  };
}

/** A profile safe to bind in forms: never null, address always present. */
export function editableProfile(p: LearnerProfile | null | undefined): LearnerProfile {
  const base = emptyLearnerProfile();
  return { ...base, ...(p ?? {}), homeAddress: { ...emptyPsgcAddress(), ...(p?.homeAddress ?? {}) } };
}

export function emptyLearnerInfo(): EnrollmentLearnerInfo {
  return {
    learnerType: null, lastGradeLevelCompleted: null, lastSchoolYearCompleted: null,
    lastSchoolAttended: null, lastSchoolAddress: null, lastSchoolId: null, track: null, strand: null,
  };
}

export function isPsgcMatched(a: PsgcAddress | null | undefined): boolean {
  return !!a?.cityMunicipalityCode && !!a?.barangayCode;
}

/** "12 Mabini St, Brgy. Alicia, Quezon City, Metro Manila" — for read-only display. */
export function formatPsgcAddress(street: string | null | undefined, a: PsgcAddress | null | undefined): string {
  const parts = [
    street?.trim() || null,
    a?.barangayName ? `Brgy. ${a.barangayName}` : null,
    a?.cityMunicipalityName ?? null,
    a?.provinceName ?? a?.regionName ?? null,
  ].filter((p): p is string => !!p);
  return parts.join(', ');
}

/** How a form decides whether a configurable field shows, is required, and what it's called. */
export interface FieldConfigView {
  visible(key: string): boolean;
  required(key: string): boolean;
  label(key: string, fallback: string): string;
}

/** Staff forms show every learner field, none forced by school form settings. */
export const ALL_FIELDS_VISIBLE: FieldConfigView = {
  visible: () => true,
  required: () => false,
  label: (_key, fallback) => fallback,
};
