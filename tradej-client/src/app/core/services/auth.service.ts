import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';

const TOKEN_KEY = 'tradej_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private http: HttpClient) {}

  login(username: string, password: string) {
    return this.http
      .post<{ token: string }>('/api/auth/login', { username, password })
      .pipe(tap(res => localStorage.setItem(TOKEN_KEY, res.token)));
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      // JWT uses Base64URL (not standard Base64) — normalise before decoding
      const base64Url = token.split('.')[1];
      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
      const padding = '='.repeat((4 - (base64.length % 4)) % 4);
      const payload = JSON.parse(atob(base64 + padding));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }
}
