import { API_ENDPOINTS, apiUrl } from "../config/api";

const readStored = (key) => sessionStorage.getItem(key) || localStorage.getItem(key) || "";

const writeStored = (key, value) => {
  if (value == null || value === "") return;
  sessionStorage.setItem(key, String(value));
  localStorage.setItem(key, String(value));
};

const unwrapProfile = (value) => {
  let current = value;
  for (let depth = 0; depth < 3; depth += 1) {
    if (!current || typeof current !== "object" || Array.isArray(current)) break;
    current = current.user ?? current.profile ?? current.data ?? current.result ?? current;
  }
  return current && typeof current === "object" && !Array.isArray(current) ? current : {};
};

const readAccessToken = (value) => {
  let current = value;
  for (let depth = 0; depth < 4; depth += 1) {
    if (!current || typeof current !== "object" || Array.isArray(current)) break;
    const token = current.token ?? current.accessToken ?? current.jwtToken ?? current.bearerToken;
    if (token) return token;
    current = current.data ?? current.result ?? current.user ?? current.profile;
  }
  return "";
};

let refreshPromise;

export const refreshAdminSession = async () => {
  if (refreshPromise) return refreshPromise;
  const oldToken = readStored("adminToken") || readStored("token");
  if (!oldToken) throw new Error("Admin session expired. Sign in again.");

  refreshPromise = (async () => {
    const response = await fetch(apiUrl(API_ENDPOINTS.auth.refreshToken), {
      method: "POST",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        Authorization: `Bearer ${oldToken}`,
        "ngrok-skip-browser-warning": "true",
      },
      credentials: "include",
      cache: "no-store",
    });
    const text = await response.text();
    let payload = {};
    if (text) {
      try { payload = JSON.parse(text); } catch { payload = {}; }
    }
    const token = readAccessToken(payload);
    if (!response.ok || !token) {
      throw new Error(payload?.message || payload?.title || "Unable to refresh Admin session. Sign in again.");
    }

    writeStored("adminToken", token);
    writeStored("token", token);
    const profile = unwrapProfile(payload);
    const hospitalId = profile.hospitalId ?? profile.HospitalId ?? profile.clinicId ?? profile.ClinicId;
    const hospitalName = profile.hospitalName ?? profile.HospitalName ?? profile.clinicName ?? profile.ClinicName;
    if (hospitalId != null) {
      writeStored("hospitalId", hospitalId);
      writeStored("clinicId", hospitalId);
    }
    if (hospitalName) {
      writeStored("hospitalName", hospitalName);
      writeStored("clinicName", hospitalName);
    }
    return token;
  })().finally(() => { refreshPromise = null; });

  return refreshPromise;
};

export const fetchCurrentAdminProfile = async () => {
  const token = readStored("adminToken") || readStored("token");
  if (!token) throw new Error("Admin session is missing. Sign in again.");
  const response = await fetch(apiUrl(API_ENDPOINTS.auth.me), {
    headers: { Accept: "application/json", Authorization: `Bearer ${token}` },
    credentials: "include",
    cache: "no-store",
  });
  const text = await response.text();
  let payload = {};
  if (text) {
    try { payload = JSON.parse(text); } catch { payload = {}; }
  }
  if (!response.ok) throw new Error(payload?.message || payload?.title || "Unable to load the signed-in Admin profile.");
  return unwrapProfile(payload);
};

export const clearAdminSession = () => {
  ["adminToken", "token", "adminRole", "userRole"].forEach((key) => {
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
  });
};
