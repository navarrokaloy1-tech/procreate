import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../../core/services/api';

interface FeatureOption {
  key: string;
  label: string;
}

export interface CustomRole {
  id: number;
  name: string;
  description: string;
  features: string[];
  labCategories: string[];
  users: number;
  createdAt: string;
}

@Component({
  selector: 'app-custom-roles',
  standalone: false,
  templateUrl: './custom-roles.html',
  styleUrl: './custom-roles.scss',
})
export class CustomRolesComponent implements OnInit {
  roles: CustomRole[] = [];
  features: FeatureOption[] = [];
  labCategoryOptions: string[] = [];
  isLoading = false;
  errorMessage = '';
  searchTerm = '';

  isModalOpen = false;
  isSaving = false;
  modalError = '';
  editingId: number | null = null;
  form = { name: '', description: '' };
  selected = new Set<string>();
  selectedLabCategories = new Set<string>();

  constructor(private api: ApiService, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadFeatures();
    this.loadLabCategories();
    this.load();
  }

  loadFeatures(): void {
    this.api.get<FeatureOption[]>('custom-roles/features').subscribe({
      next: (f) => {
        this.features = f;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  loadLabCategories(): void {
    this.api.get<string[]>('custom-roles/lab-categories').subscribe({
      next: (c) => {
        this.labCategoryOptions = c;
        this.cdr.markForCheck();
      },
      error: () => this.cdr.markForCheck(),
    });
  }

  load(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.api.get<CustomRole[]>('custom-roles').subscribe({
      next: (roles) => {
        this.roles = roles;
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.errorMessage = 'Could not load custom roles.';
        this.isLoading = false;
        this.cdr.markForCheck();
      },
    });
  }

  get filteredRoles(): CustomRole[] {
    const q = this.searchTerm.trim().toLowerCase();
    if (!q) return this.roles;
    return this.roles.filter(
      (r) => r.name.toLowerCase().includes(q) || r.description.toLowerCase().includes(q)
    );
  }

  featureLabel(key: string): string {
    return this.features.find((f) => f.key === key)?.label ?? key;
  }

  // ----------------------------------------------------------
  // Create / edit
  // ----------------------------------------------------------

  openAdd(): void {
    this.editingId = null;
    this.form = { name: '', description: '' };
    this.selected = new Set<string>();
    this.selectedLabCategories = new Set<string>();
    this.modalError = '';
    this.isModalOpen = true;
  }

  openEdit(role: CustomRole): void {
    this.editingId = role.id;
    this.form = { name: role.name, description: role.description };
    this.selected = new Set(role.features);
    this.selectedLabCategories = new Set(role.labCategories ?? []);
    this.modalError = '';
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.modalError = '';
    this.isSaving = false;
  }

  toggleFeature(key: string): void {
    if (this.selected.has(key)) this.selected.delete(key);
    else this.selected.add(key);
  }

  isFeatureSelected(key: string): boolean {
    return this.selected.has(key);
  }

  toggleLabCategory(name: string): void {
    if (this.selectedLabCategories.has(name)) this.selectedLabCategories.delete(name);
    else this.selectedLabCategories.add(name);
  }

  isLabCategorySelected(name: string): boolean {
    return this.selectedLabCategories.has(name);
  }

  /** Whether the Laboratory feature is granted — lab scoping only matters then. */
  get hasLabFeature(): boolean {
    return this.selected.has('laboratory');
  }

  save(): void {
    if (!this.form.name.trim()) {
      this.modalError = 'Enter a role name.';
      return;
    }
    if (this.selected.size === 0) {
      this.modalError = 'Select at least one feature.';
      return;
    }

    const payload = {
      name: this.form.name.trim(),
      description: this.form.description.trim(),
      features: Array.from(this.selected),
      // Only meaningful with the Laboratory feature; cleared otherwise so a
      // role that loses lab access does not keep a stale scope.
      labCategories: this.hasLabFeature ? Array.from(this.selectedLabCategories) : [],
    };

    this.isSaving = true;
    const request: Observable<any> = this.editingId
      ? this.api.put(`custom-roles/${this.editingId}`, payload)
      : this.api.post('custom-roles', payload);

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.closeModal();
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.isSaving = false;
        this.modalError = err?.error?.message ?? 'Saving failed. Please try again.';
        this.cdr.markForCheck();
      },
    });
  }

  remove(role: CustomRole): void {
    const extra = role.users > 0
      ? ` ${role.users} account${role.users === 1 ? '' : 's'} will fall back to their built-in role.`
      : '';
    if (!window.confirm(`Delete the "${role.name}" role?${extra}`)) return;

    this.api.delete(`custom-roles/${role.id}`).subscribe({
      next: () => {
        this.load();
        this.cdr.markForCheck();
      },
      error: (err) => {
        window.alert(err?.error?.message ?? 'Could not delete the role.');
        this.cdr.markForCheck();
      },
    });
  }
}
