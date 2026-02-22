import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ReleaseDto, ReleaseListDto, ReleaseStatisticsDto } from '../contracts/release.contracts';

@Injectable({
  providedIn: 'root'
})
export class ReleaseApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/releases`;
  private readonly adminUrl = `${environment.apiUrl}/api/admin`;

  getAll(): Observable<ReleaseListDto[]> {
    return this.http.get<ReleaseListDto[]>(this.baseUrl);
  }

  getById(id: string): Observable<ReleaseDto> {
    return this.http.get<ReleaseDto>(`${this.baseUrl}/${id}`);
  }

  getStatistics(): Observable<ReleaseStatisticsDto> {
    return this.http.get<ReleaseStatisticsDto>(`${this.baseUrl}/statistics`);
  }

  seedDemoReleases(): Observable<any> {
    return this.http.post<any>(`${this.adminUrl}/demo/seed-releases`, {});
  }
}
