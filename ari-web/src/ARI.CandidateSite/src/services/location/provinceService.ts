import axios from 'axios'

/**
 * Provinces Open API v2 — đơn vị hành chính Việt Nam SAU sáp nhập 07/2025 (2 cấp: Tỉnh → Phường/Xã).
 * Tài liệu: https://provinces.open-api.vn/
 */
const BASE = 'https://provinces.open-api.vn/api/v2'

export interface Province {
  code: number
  name: string
  divisionType?: string
}

export interface Ward {
  code: number
  name: string
  province_code: number
}

/** Thành phố trực thuộc trung ương — tên rút gọn (bỏ tiền tố "Thành phố"). */
export interface City {
  code: number
  /** Tên đầy đủ từ API, vd "Thành phố Hà Nội". */
  fullName: string
  /** Tên rút gọn dùng để hiển thị & so khớp DB, vd "Hà Nội". */
  name: string
}

// Cache đơn giản trong phiên để tránh gọi lặp.
let provincesCache: Province[] | null = null
let citiesCache: City[] | null = null
let locationsCache: City[] | null = null
const wardsCache = new Map<number, Ward[]>()

/** Rút gọn "Thành phố Hà Nội" -> "Hà Nội", "Tỉnh Bắc Ninh" -> "Bắc Ninh". */
function stripPrefix(name: string): string {
  return name.replace(/^(Thành phố|Tỉnh)\s+/i, '').trim()
}

interface RawProvince {
  code: number
  name: string
  division_type?: string
}

export const provinceService = {
  async getProvinces(): Promise<Province[]> {
    if (provincesCache) return provincesCache
    const { data } = await axios.get<RawProvince[]>(`${BASE}/p/`)
    provincesCache = data
      .map((p) => ({ code: p.code, name: p.name, divisionType: p.division_type }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
    return provincesCache
  },

  /** Chỉ các thành phố trực thuộc trung ương (Hà Nội, Đà Nẵng, ...), tên rút gọn. */
  async getCities(): Promise<City[]> {
    if (citiesCache) return citiesCache
    const provinces = await this.getProvinces()
    citiesCache = provinces
      .filter((p) => (p.divisionType ?? '').toLowerCase().includes('thành phố trung ương'))
      .map((p) => ({ code: p.code, fullName: p.name, name: stripPrefix(p.name) }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
    return citiesCache
  },

  /** Toàn bộ tỉnh/thành (cả thành phố lẫn tỉnh), tên rút gọn — dùng cho bộ lọc địa điểm. */
  async getLocations(): Promise<City[]> {
    if (locationsCache) return locationsCache
    const provinces = await this.getProvinces()
    locationsCache = provinces
      .map((p) => ({ code: p.code, fullName: p.name, name: stripPrefix(p.name) }))
      .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
    return locationsCache
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
