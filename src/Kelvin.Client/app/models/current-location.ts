export type CurrentLocation = {
  id: number;
  name: string;
  latitude: number;
  longitude: number;
  elevation: number | null;
  timeZone: string | null;
  country: string | null;
  countryCode: string | null;
  admin1: string | null;
  admin2: string | null;
  admin3: string | null;
  postCodes: string[];
};
