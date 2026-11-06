import {
  Card,
  CardBody,
  CardHeader,
  Field,
  Input,
  Select,
  Switch,
} from "../../components/ui";
import type { GratuityWage, PayrollCountry } from "../../types";

/** Gulf payroll settings held as text while editing. */
export interface GulfForm {
  payrollCountry: PayrollCountry;
  currency: string;
  gosiAnnuityEmployeePercent: string;
  gosiAnnuityEmployerPercent: string;
  gosiSanedEmployeePercent: string;
  gosiSanedEmployerPercent: string;
  gosiHazardEmployerPercent: string;
  gosiWageFloor: string;
  gosiWageCeiling: string;
  gosiNewEntrantStepPercent: string;
  gosiNewEntrantMaxPercent: string;
  pensionEmployeePercent: string;
  pensionEmployerPercent: string;
  pensionWageFloor: string;
  pensionWageCeiling: string;
  pensionOnGross: boolean;
  gulfGratuityWage: GratuityWage;
  wpsEmployerId: string;
  wpsBankRoutingCode: string;
  wpsEmployerIban: string;
  wpsAgentId: string;
}

export const COUNTRY_CURRENCY: Record<PayrollCountry, string> = {
  PK: "PKR",
  AE: "AED",
  SA: "SAR",
};

function Pct({
  label,
  value,
  onChange,
  max = 30,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  max?: number;
}) {
  return (
    <Field label={label}>
      <Input
        type="number"
        min={0}
        max={max}
        step="0.01"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </Field>
  );
}

/** Country and currency: Pakistan by default; UAE and Saudi switch to the Gulf rules below. */
export function CountryCard({
  g,
  set,
}: {
  g: GulfForm;
  set: (p: Partial<GulfForm>) => void;
}) {
  return (
    <Card className="max-w-2xl">
      <CardHeader
        title="Country and currency"
        description="Pakistan uses FBR income tax, EOBI, provident fund and VPS. UAE and Saudi Arabia have no income tax and use GPSSA or GOSI, end-of-service gratuity and WPS salary files."
      />
      <CardBody className="grid gap-4 sm:grid-cols-2">
        <Field label="Payroll country">
          <Select
            value={g.payrollCountry}
            onChange={(e) => {
              const c = e.target.value as PayrollCountry;
              set({ payrollCountry: c, currency: COUNTRY_CURRENCY[c] });
            }}
          >
            <option value="PK">Pakistan</option>
            <option value="AE">United Arab Emirates</option>
            <option value="SA">Saudi Arabia</option>
          </Select>
        </Field>
        <Field label="Currency" hint="Shown on payslips, reports and screens">
          <Select
            value={g.currency}
            onChange={(e) => set({ currency: e.target.value })}
          >
            {["PKR", "AED", "SAR", "USD"].map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </Select>
        </Field>
      </CardBody>
    </Card>
  );
}

/** GOSI or GPSSA, the gratuity wage and the WPS file details for UAE and Saudi payroll. */
export function GulfCards({
  g,
  set,
}: {
  g: GulfForm;
  set: (p: Partial<GulfForm>) => void;
}) {
  const saudi = g.payrollCountry === "SA";
  return (
    <>
      {saudi ? (
        <Card className="mt-6 max-w-2xl">
          <CardHeader
            title="GOSI (Saudi social insurance)"
            description="On basic + housing. Saudis: annuities and SANED from both sides plus 2% occupational hazards from the company. Others: hazards only. Tick Saudi nationality on each person's file."
          />
          <CardBody className="grid gap-4 sm:grid-cols-2">
            <Pct
              label="Annuities, employee (%)"
              value={g.gosiAnnuityEmployeePercent}
              onChange={(v) => set({ gosiAnnuityEmployeePercent: v })}
            />
            <Pct
              label="Annuities, company (%)"
              value={g.gosiAnnuityEmployerPercent}
              onChange={(v) => set({ gosiAnnuityEmployerPercent: v })}
            />
            <Pct
              label="SANED, employee (%)"
              value={g.gosiSanedEmployeePercent}
              onChange={(v) => set({ gosiSanedEmployeePercent: v })}
              max={10}
            />
            <Pct
              label="SANED, company (%)"
              value={g.gosiSanedEmployerPercent}
              onChange={(v) => set({ gosiSanedEmployerPercent: v })}
              max={10}
            />
            <Pct
              label="Occupational hazards, company (%)"
              value={g.gosiHazardEmployerPercent}
              onChange={(v) => set({ gosiHazardEmployerPercent: v })}
              max={10}
            />
            <div />
            <Field label="Lowest wage counted (SAR)">
              <Input
                type="number"
                min={0}
                value={g.gosiWageFloor}
                onChange={(e) => set({ gosiWageFloor: e.target.value })}
              />
            </Field>
            <Field label="Highest wage counted (SAR)">
              <Input
                type="number"
                min={0}
                value={g.gosiWageCeiling}
                onChange={(e) => set({ gosiWageCeiling: e.target.value })}
              />
            </Field>
            <Pct
              label="New entrants: yearly rise each July (%)"
              value={g.gosiNewEntrantStepPercent}
              onChange={(v) => set({ gosiNewEntrantStepPercent: v })}
              max={5}
            />
            <Pct
              label="New entrants: annuities up to (%)"
              value={g.gosiNewEntrantMaxPercent}
              onChange={(v) => set({ gosiNewEntrantMaxPercent: v })}
            />
            <p className="text-xs text-zinc-500 sm:col-span-2 dark:text-zinc-400">
              Saudis first registered with GOSI from 3 July 2024 pay a rising
              annuity rate (from July 2025). Tick “GOSI new entrant” on their
              file.
            </p>
          </CardBody>
        </Card>
      ) : (
        <Card className="mt-6 max-w-2xl">
          <CardHeader
            title="Pension for UAE nationals (GPSSA)"
            description="Deducted only for people whose file says they are UAE nationals. Abu Dhabi companies on ADPF can change the rates here."
          />
          <CardBody className="grid gap-4 sm:grid-cols-2">
            <Pct
              label="Employee share (%)"
              value={g.pensionEmployeePercent}
              onChange={(v) => set({ pensionEmployeePercent: v })}
            />
            <Pct
              label="Company share (%)"
              value={g.pensionEmployerPercent}
              onChange={(v) => set({ pensionEmployerPercent: v })}
            />
            <Field label="Lowest salary counted (AED)">
              <Input
                type="number"
                min={0}
                value={g.pensionWageFloor}
                onChange={(e) => set({ pensionWageFloor: e.target.value })}
              />
            </Field>
            <Field label="Highest salary counted (AED)">
              <Input
                type="number"
                min={0}
                value={g.pensionWageCeiling}
                onChange={(e) => set({ pensionWageCeiling: e.target.value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Switch
                checked={g.pensionOnGross}
                onChange={(v) => set({ pensionOnGross: v })}
                label="On gross salary"
                description="Off: on basic + housing."
              />
            </div>
          </CardBody>
        </Card>
      )}
      <Card className="mt-6 max-w-2xl">
        <CardHeader
          title="End-of-service gratuity"
          description={
            saudi
              ? "Saudi law: half a month per year for the first 5 years, a full month after. Resigning pays nothing under 2 years, a third up to 5, two thirds up to 10. Added to the final settlement."
              : "UAE law: 21 days per year for the first 5 years, 30 days after, up to two years’ pay; resigning does not reduce it. Added to the final settlement."
          }
        />
        <CardBody>
          <Field label="Worked out on" className="max-w-xs">
            <Select
              value={g.gulfGratuityWage}
              onChange={(e) =>
                set({ gulfGratuityWage: e.target.value as GratuityWage })
              }
            >
              <option value="Basic">Basic salary</option>
              <option value="BasicPlusHousing">Basic + housing</option>
              <option value="Gross">Gross salary</option>
            </Select>
          </Field>
        </CardBody>
      </Card>
      <Card className="mt-6 max-w-2xl">
        <CardHeader
          title="WPS salary file"
          description={
            saudi
              ? "For the bank / Mudad payroll file. Each person needs an IBAN and Iqama or national ID on their file."
              : "For the SIF file your bank or exchange house uploads. Each person needs an IBAN and labour card (MOHRE person ID) on their file."
          }
        />
        <CardBody className="grid gap-4 sm:grid-cols-2">
          <Field
            label={
              saudi
                ? "Establishment number (Mudad)"
                : "Employer ID (13 digits, MOHRE)"
            }
          >
            <Input
              value={g.wpsEmployerId}
              maxLength={20}
              onChange={(e) => set({ wpsEmployerId: e.target.value })}
            />
          </Field>
          <Field label="Company IBAN">
            <Input
              value={g.wpsEmployerIban}
              maxLength={34}
              onChange={(e) => set({ wpsEmployerIban: e.target.value })}
            />
          </Field>
          {!saudi && (
            <>
              <Field label="Employer bank routing code">
                <Input
                  value={g.wpsBankRoutingCode}
                  maxLength={20}
                  onChange={(e) => set({ wpsBankRoutingCode: e.target.value })}
                />
              </Field>
              <Field
                label="WPS agent ID"
                hint="Used when a person has no bank routing code"
              >
                <Input
                  value={g.wpsAgentId}
                  maxLength={20}
                  onChange={(e) => set({ wpsAgentId: e.target.value })}
                />
              </Field>
            </>
          )}
        </CardBody>
      </Card>
    </>
  );
}
