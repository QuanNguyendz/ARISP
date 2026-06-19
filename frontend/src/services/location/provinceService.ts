import axios from 'axios'

/**
 * Provinces Open API v2 — đơn vị hành chính Việt Nam SAU sáp nhập 07/2025 (2 cấp: Tỉnh → Phường/Xã).
 * Tài liệu: https://provinces.open-api.vn/
 */
const BASE = 'https://provinces.open-api.vn/api/v2'

export interface Province {
  code: number
  name: string
}

export interface Ward {
  code: number
  name: string
  province_code: number
}

// Cache đơn giản trong phiên để tránh gọi lặp.
let provincesCache: Province[] | null = null
const wardsCache = new Map<number, Ward[]>()

export const provinceService = {
  async getProvinces(): Promise<Province[]> {
    if (provincesCache) return provincesCache
    const { data } = await axios.get<Province[]>(`${BASE}/p/`)
    provincesCache = data
      .map((p) => ({ code: p.code, name: p.name }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
    return provincesCache
  },

  async getWards(provinceCode: number): Promise<Ward[]> {
    const cached = wardsCache.get(provinceCode)
    if (cached) return cached
    const { data } = await axios.get<{ wards?: Ward[] }>(`${BASE}/p/${provinceCode}?depth=2`)
    const wards = (data.wards ?? [])
      .map((w) => ({ code: w.code, name: w.name, province_code: provinceCode }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
    wardsCache.set(provinceCode, wards)
    return wards
  },
}
