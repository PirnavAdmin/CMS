import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Clock3,
  HeartPulse,
  RefreshCw,
  ShieldCheck,
  XCircle,
} from "lucide-react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  createSubscriptionCheckout,
  fetchSubscriptionPaymentMethods,
  fetchSubscriptionPaymentStatus,
  fetchSubscriptionPlans,
  fetchMySubscription,
  hasVerifiedAdminSession,
  getSubscriptionPurchaseId,
  initiateSubscriptionPayment,
} from "../utils/subscriptionFlow";
import { fetchCurrentAdminProfile } from "../utils/adminAuthSession";
import { formatIndianCurrency } from "../utils/format";
import "./LandingPage.css";

const getPaymentOutcome = (response) => {
  const statuses = [
    response?.paymentStatus,
    response?.PaymentStatus,
    response?.purchaseStatus,
    response?.PurchaseStatus,
    response?.status,
    response?.Status,
    response?.result,
    response?.Result,
    response?.payment?.status,
    response?.Payment?.Status,
    response?.purchase?.paymentStatus,
    response?.Purchase?.PaymentStatus,
    response?.purchase?.status,
    response?.Purchase?.Status,
    response?.payment?.paymentStatus,
    response?.Payment?.PaymentStatus,
    response?.data?.paymentStatus,
    response?.data?.PaymentStatus,
    response?.data?.purchaseStatus,
    response?.data?.PurchaseStatus,
    response?.data?.purchase?.paymentStatus,
    response?.data?.purchase?.status,
    response?.data?.status,
    response?.data?.Status,
    response?.data?.result,
    response?.data?.payment?.status,
  ].filter((value) => value !== undefined && value !== null);
  const normalized = statuses.map((value) => String(value).trim().toLowerCase());
  const messages = [
    response?.message,
    response?.Message,
    response?.data?.message,
    response?.data?.Message,
  ].filter(Boolean).map((value) => String(value).trim().toLowerCase());
  const pendingPayment = [...normalized, ...messages].some((value) =>
    /\b(payment|transaction)\b.*\b(pending|initiated|processing)\b/.test(value)
  );
  const rejectedStatus = normalized.some((value) =>
    ["rejected", "failed", "failure", "declined", "cancelled", "canceled"].includes(value)
  );
  const rejectedMessage = messages.some((value) =>
    /\b(payment|transaction)\b.*\b(rejected|failed|declined|cancelled|canceled)\b/.test(value)
  );
  if (rejectedStatus || rejectedMessage || response?.isApproved === false || response?.IsApproved === false) return "rejected";
  if (
    normalized.some((value) =>
      ["approved", "success", "successful", "succeeded", "paid", "completed", "verified"].includes(value)
    ) ||
    response?.isApproved === true ||
    response?.IsApproved === true
  ) return "approved";
  if (pendingPayment || normalized.some((value) => ["pending", "initiated", "processing", "in progress"].includes(value))) return "pending";
  if (response?.success === false || response?.Success === false) return "rejected";
  return "unknown";
};

const getPaymentMessage = (response, fallback) =>
  response?.message || response?.Message || response?.data?.message || response?.data?.Message || fallback;

export default function SubscriptionCheckoutPage() {
  const { planId } = useParams();
  const navigate = useNavigate();
  const [plan, setPlan] = useState(null);
  const [plans, setPlans] = useState([]);
  const [accessModule, setAccessModule] = useState("lab");
  const [loading, setLoading] = useState(true);
  const [paymentMethods, setPaymentMethods] = useState([]);
  const [gatewayConfigured, setGatewayConfigured] = useState(false);
  const [merchantSetupOpen, setMerchantSetupOpen] = useState(false);
  const [checkingPaymentConfiguration, setCheckingPaymentConfiguration] = useState(false);
  const [adminRole, setAdminRole] = useState("");
  const [paymentMethodsError, setPaymentMethodsError] = useState("");
  const [details, setDetails] = useState({
    name: "",
    email: "",
    phone: "",
  });
  const [paymentMethod, setPaymentMethod] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [purchaseId, setPurchaseId] = useState("");
  const [paymentOutcome, setPaymentOutcome] = useState(null);
  const [checkingPaymentStatus, setCheckingPaymentStatus] = useState(false);
  const [subscriptionActive, setSubscriptionActive] = useState(false);
  const paymentSubmitLock = useRef(false);
  const configurationRetryLock = useRef(false);

  useEffect(() => {
    let active = true;
    const readSessionValue = (key) => sessionStorage.getItem(key) || localStorage.getItem(key) || "";
    if (!hasVerifiedAdminSession()) {
      navigate("/login", { replace: true, state: { returnTo: window.location.pathname } });
      return () => { active = false; };
    }

    Promise.all([fetchCurrentAdminProfile(), fetchSubscriptionPlans(), fetchSubscriptionPaymentMethods()])
      .then(([profile, items, paymentOptions]) => {
        if (!active) return;
        const clinicId = profile.hospitalId || profile.HospitalId || profile.clinicId || profile.ClinicId || readSessionValue("hospitalId") || readSessionValue("clinicId");
        if (!clinicId) {
          navigate("/clinic/setup", { replace: true });
          return;
        }
        const name = profile.name ?? profile.fullName ?? profile.adminName ?? profile.Name ?? readSessionValue("adminName");
        const email = profile.email ?? profile.adminEmail ?? profile.Email ?? readSessionValue("adminEmail");
        setAdminRole(String(profile.role ?? profile.Role ?? readSessionValue("adminRole") ?? readSessionValue("userRole") ?? ""));
        const phone = profile.mobileNumber ?? profile.phoneNumber ?? profile.phone ?? profile.MobileNumber ?? readSessionValue("mobileNumber");
        setDetails((current) => ({ name: name || current.name, email: email || current.email, phone: phone || current.phone }));
        const activePlans = items.filter((item) => item.isActive);
        const selected = activePlans.find((item) => String(item.id) === String(planId));
        setPlans(activePlans);
        setPlan(selected);
        setPaymentMethods(paymentOptions.methods);
        setGatewayConfigured(paymentOptions.gatewayConfigured);
        setPaymentMethod((current) =>
          paymentOptions.methods.some((method) => method.code === current && method.enabled)
            ? current
            : paymentOptions.methods.find((method) => method.enabled)?.code || ""
        );
      })
      .catch((failure) => {
        if (!active) return;
        setPlans([]);
        setPlan(null);
        setPaymentMethods([]);
        setPaymentMethodsError(failure.message || "Unable to load the signed-in Admin, plans, and payment methods.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [planId, navigate]);

  useEffect(() => {
    const refreshAvailability = () => {
      if (document.visibilityState !== "visible") return;
      fetchSubscriptionPaymentMethods().then((configuration) => {
        setPaymentMethods(configuration.methods);
        setGatewayConfigured(configuration.gatewayConfigured);
        setPaymentMethod((current) => configuration.methods.some((method) => method.code === current && method.enabled)
          ? current
          : configuration.methods.find((method) => method.enabled)?.code || "");
      }).catch(() => {});
    };
    window.addEventListener("focus", refreshAvailability);
    document.addEventListener("visibilitychange", refreshAvailability);
    return () => {
      window.removeEventListener("focus", refreshAvailability);
      document.removeEventListener("visibilitychange", refreshAvailability);
    };
  }, []);

  const accessVariants = useMemo(() => {
    if (!plan) return { without: null, with: null };
    const matching = plans.filter(
      (item) =>
        item.planType === plan.planType &&
        Number(item.durationMonths) === Number(plan.durationMonths)
    );
    const accessKey = accessModule === "lab" ? "includesLab" : "includesPharmacy";
    const otherAccessKey = accessModule === "lab" ? "includesPharmacy" : "includesLab";
    const findVariant = (includesAccess) =>
      matching.find((item) => item[accessKey] === includesAccess && item[otherAccessKey] === plan[otherAccessKey]) ||
      matching.find((item) => item[accessKey] === includesAccess) ||
      null;
    return {
      without: findVariant(false),
      with: findVariant(true),
    };
  }, [accessModule, plan, plans]);

  const selectAccessOption = (includesAccess) => {
    const selected = includesAccess ? accessVariants.with : accessVariants.without;
    if (!selected) return;
    setPlan(selected);
    setConfirmed(false);
    sessionStorage.setItem(
      "cmsSelectedSubscriptionPlan",
      JSON.stringify(selected)
    );
  };

  const submit = async (event) => {
    event.preventDefault();
    if (paymentSubmitLock.current) return;
    if (!hasVerifiedAdminSession()) {
      setSubmitError("Sign in with a verified Admin account before starting subscription checkout.");
      return;
    }
    paymentSubmitLock.current = true;
    setSubmitting(true);
    setSubmitError("");
    setPaymentMethodsError("");
    const request = {
      plan,
      includesLab: Boolean(plan.includesLab),
      customer: details,
      paymentMethod,
      paymentConfirmed: confirmed,
    };
    let activePurchaseId = purchaseId;
    try {
      setCheckingPaymentConfiguration(true);
      const latestConfiguration = await fetchSubscriptionPaymentMethods();
      setPaymentMethods(latestConfiguration.methods);
      setGatewayConfigured(latestConfiguration.gatewayConfigured);
      setPaymentMethod((current) => latestConfiguration.methods.some((method) => method.code === current && method.enabled)
        ? current
        : latestConfiguration.methods.find((method) => method.enabled)?.code || "");
      setCheckingPaymentConfiguration(false);
      if (!latestConfiguration.gatewayConfigured) {
        setMerchantSetupOpen(true);
        return;
      }
      if (!latestConfiguration.methods.some((method) => method.code === paymentMethod && method.enabled)) {
        setSubmitError(latestConfiguration.methods.some((method) => method.enabled)
          ? "Payment options were updated. Select an available payment method and try again."
          : "No payment methods are currently available. Please contact the CMS Owner.");
        return;
      }
      if (!activePurchaseId) {
        const purchase = await createSubscriptionCheckout({
          name: details.name,
          email: details.email,
          mobileNumber: details.phone,
          subscriptionPlanId: plan.id,
          paymentMethod,
        });
        activePurchaseId = getSubscriptionPurchaseId(purchase);
        if (!activePurchaseId) {
          throw new Error("The checkout was created, but the API response did not include a purchase ID. Contact support before retrying to avoid creating a duplicate purchase.");
        }
        setPurchaseId(activePurchaseId);
      }
      const payment = await initiateSubscriptionPayment(activePurchaseId, paymentMethod);
      const initiatedOutcome = {
        status: getPaymentOutcome(payment),
        message: getPaymentMessage(payment, "The payment service returned no status details."),
      };
      setPaymentOutcome(initiatedOutcome);
      try {
        const status = await fetchSubscriptionPaymentStatus(activePurchaseId);
        const statusOutcome = getPaymentOutcome(status);
        const confirmedOutcome = statusOutcome === "unknown" ? initiatedOutcome.status : statusOutcome;
        setPaymentOutcome({
          status: confirmedOutcome,
          message: getPaymentMessage(status, initiatedOutcome.message),
        });
        if (confirmedOutcome === "approved") {
          try {
            const currentSubscription = await fetchMySubscription();
            const activeSubscription = String(currentSubscription?.status || "").trim().toLowerCase() === "active" && currentSubscription?.includesCms === true;
            setSubscriptionActive(activeSubscription);
            if (activeSubscription) {
              window.dispatchEvent(new Event("cms:subscription-updated"));
              navigate("/dashboard", { replace: true });
            }
            else setPaymentOutcome((current) => ({ ...current, activationPending: true }));
          } catch {
            setPaymentOutcome((current) => ({ ...current, activationPending: true }));
          }
        }
      } catch (statusFailure) {
        setPaymentOutcome({
          ...initiatedOutcome,
          statusCheckError: statusFailure.message || "Unable to confirm the payment status.",
        });
      }
      sessionStorage.setItem(
        "cmsSelectedSubscriptionPlan",
        JSON.stringify(plan)
      );
      sessionStorage.setItem(
        "cmsSubscriptionRequest",
        JSON.stringify(request)
      );
    } catch (failure) {
      const rejected = getPaymentOutcome(failure.payload) === "rejected" || failure.status === 402;
      if (activePurchaseId && rejected) {
        setPaymentOutcome({
          status: "rejected",
          message: getPaymentMessage(failure.payload, failure.message || "The payment was rejected."),
        });
      } else {
        setSubmitError(failure.message || "Unable to submit the subscription request.");
      }
    } finally {
      setCheckingPaymentConfiguration(false);
      setSubmitting(false);
      paymentSubmitLock.current = false;
    }
  };
  const retryPaymentConfiguration = async () => {
    if (configurationRetryLock.current) return;
    configurationRetryLock.current = true;
    setCheckingPaymentConfiguration(true);
    setPaymentMethodsError("");
    try {
      const latestConfiguration = await fetchSubscriptionPaymentMethods();
      setPaymentMethods(latestConfiguration.methods);
      setGatewayConfigured(latestConfiguration.gatewayConfigured);
      setPaymentMethod((current) => latestConfiguration.methods.some((method) => method.code === current && method.enabled)
        ? current
        : latestConfiguration.methods.find((method) => method.enabled)?.code || "");
      if (latestConfiguration.gatewayConfigured) setMerchantSetupOpen(false);
      else setPaymentMethodsError("Razorpay merchant setup is still required before payment can proceed.");
    } catch (failure) {
      setPaymentMethodsError(failure.message || "Unable to refresh payment configuration. Please retry.");
    } finally {
      setCheckingPaymentConfiguration(false);
      configurationRetryLock.current = false;
    }
  };
  const checkPaymentStatus = async () => {
    if (!purchaseId || checkingPaymentStatus) return;
    setCheckingPaymentStatus(true);
    try {
      const status = await fetchSubscriptionPaymentStatus(purchaseId);
      const statusOutcome = getPaymentOutcome(status);
      setPaymentOutcome({
        status: statusOutcome,
        message: getPaymentMessage(status, "The payment service returned no status details."),
      });
      if (statusOutcome === "approved") {
        const currentSubscription = await fetchMySubscription().catch(() => null);
        const activeSubscription = String(currentSubscription?.status || "").trim().toLowerCase() === "active" && currentSubscription?.includesCms === true;
        setSubscriptionActive(activeSubscription);
        if (activeSubscription) {
          window.dispatchEvent(new Event("cms:subscription-updated"));
          navigate("/dashboard", { replace: true });
        }
        else setPaymentOutcome((current) => ({ ...current, activationPending: true }));
      }
    } catch (failure) {
      setPaymentOutcome((current) => ({
        ...current,
        statusCheckError: failure.message || "Unable to confirm the payment status.",
      }));
    } finally {
      setCheckingPaymentStatus(false);
    }
  };
  const continueAfterApproval = () => {
    navigate(subscriptionActive ? "/dashboard" : "/subscription", { replace: true });
  };

  return (
    <main className="subscription-page">
      <header className="subscription-page-nav">
        <Link to="/" className="subscription-page-brand">
          <HeartPulse size={24} />
          <span>
            <b>CMS</b>
            <small>Clinical Intelligence System</small>
          </span>
        </Link>
        <Link to="/plans" className="subscription-back">
          <ArrowLeft size={16} /> Change plan
        </Link>
      </header>

      {loading ? (
        <div className="landing-plan-state">Loading selected plan...</div>
      ) : !plan ? (
        <div className="subscription-missing">
          <h2>{paymentMethodsError ? "Unable to load checkout" : "Plan not found"}</h2>
          {paymentMethodsError ? <p role="alert">{paymentMethodsError}</p> : null}
          <Link to="/plans">View available plans</Link>
        </div>
      ) : (
        <section className="subscription-form-page">
          {!hasVerifiedAdminSession() ? (
            <div className="landing-payment-error" role="status">
              Subscription checkout requires a verified Admin account. <Link to="/login">Sign in as Admin</Link>, then return to this plan.
            </div>
          ) : null}
          <div className="landing-checkout">
            <aside className="landing-checkout-summary">
              <span>Selected subscription</span>
              <h3>
                {plan.planType} - {plan.durationMonths}{" "}
                {plan.durationMonths === 1 ? "month" : "months"}
              </h3>
              <strong>{formatIndianCurrency(plan.price)}</strong>
              <p>{plan.name}</p><p>Branches: {plan.maxBranches ?? "Not specified"} ? Appointments/day: {plan.dailyAppointmentLimit ?? "Not specified"}</p><ul>{plan.benefits.map(benefit => <li key={benefit}>{benefit}</li>)}</ul>
              <p className="landing-lab-summary">
                {plan.includesLab ? "With Lab" : "Without Lab"}
              </p>
            </aside>

            {paymentOutcome ? (
              <section
                className={`landing-payment-result is-${paymentOutcome.status}`}
                role={paymentOutcome.status === "rejected" ? "alert" : "status"}
                aria-live="polite"
              >
                {paymentOutcome.status === "approved" ? <CheckCircle2 size={42} /> : null}
                {paymentOutcome.status === "rejected" ? <XCircle size={42} /> : null}
                {paymentOutcome.status === "pending" || paymentOutcome.status === "unknown" ? <Clock3 size={42} /> : null}
                <h2>
                  {paymentOutcome.status === "approved"
                    ? "Payment Approved"
                    : paymentOutcome.status === "rejected"
                      ? "Payment Rejected"
                      : paymentOutcome.status === "pending"
                        ? "Payment Pending"
                        : "Payment Status Unconfirmed"}
                </h2>
                <p>{paymentOutcome.message}</p>
                {paymentOutcome.statusCheckError ? <p className="landing-payment-error" role="alert">Payment initiation was recorded, but the payment status could not be confirmed: {paymentOutcome.statusCheckError}</p> : null}
                {paymentOutcome.status === "approved" ? (
                  <>
                    <p className="landing-payment-result-note">Purchase ID: {purchaseId}. {paymentOutcome.activationPending ? "Payment is verified and the subscription is awaiting activation. Refresh the subscription page to check access." : subscriptionActive ? "Your subscription is active." : "Checking subscription activation."}</p>
                    <button type="button" onClick={continueAfterApproval}>
                      {subscriptionActive ? "Continue to clinic" : "Check subscription"} <ArrowRight size={17} />
                    </button>
                  </>
                ) : paymentOutcome.status === "rejected" ? (
                  <button type="button" onClick={() => setPaymentOutcome(null)}>
                    Try payment again
                  </button>
                ) : (
                  <>
                    <p className="landing-payment-result-note">Purchase ID: {purchaseId}. Do not create another checkout; check this purchase status before retrying.</p>
                    <button type="button" onClick={checkPaymentStatus} disabled={checkingPaymentStatus}>
                      {checkingPaymentStatus ? "Checking status..." : "Check payment status"}
                    </button>
                  </>
                )}
              </section>
            ) : (
            <form className="landing-checkout-form" onSubmit={submit}>
              <fieldset className="landing-lab-options">
                <legend>
                  <span>Choose {accessModule === "lab" ? "lab" : "pharmacy"} access</span>
                  <label className="landing-access-module-select">
                    <span>Choose module</span>
                    <select value={accessModule} onChange={(event) => setAccessModule(event.target.value)}>
                      <option value="lab">Lab</option>
                      <option value="pharmacy">Pharmacy</option>
                    </select>
                  </label>
                </legend>
                {[
                  {
                    label: accessModule === "lab" ? "Without Lab" : "Without Pharmacy",
                    includesAccess: false,
                    variant: accessVariants.without,
                  },
                  {
                    label: accessModule === "lab" ? "With Lab" : "With Pharmacy",
                    includesAccess: true,
                    variant: accessVariants.with,
                  },
                ].map((option) => (
                  <label
                    key={option.label}
                    className={
                      option.variant &&
                      String(option.variant.id) === String(plan.id)
                        ? "is-selected"
                        : ""
                    }
                  >
                    <input
                      type="radio"
                      name="subscriptionAccess"
                      checked={Boolean(option.variant && String(option.variant.id) === String(plan.id))}
                      disabled={!option.variant}
                      onChange={() => selectAccessOption(option.includesAccess)}
                    />
                    <span>
                      <b>{option.label}</b>
                      <small>
                        {option.variant
                          ? formatIndianCurrency(option.variant.price)
                          : "Not configured"}
                      </small>
                    </span>
                  </label>
                ))}
              </fieldset>

              <h3>Basic details</h3>
              <div className="landing-checkout-fields">
                <label>
                  Full name
                  <input
                    required
                    autoComplete="name"
                    value={details.name}
                    onChange={(event) =>
                      setDetails({ ...details, name: event.target.value })
                    }
                  />
                </label>
                <label>
                  Email address
                  <input
                    required
                    type="email"
                    autoComplete="email"
                    maxLength={254}
                    value={details.email}
                    onChange={(event) =>
                      setDetails({ ...details, email: event.target.value })
                    }
                  />
                </label>
                <label>
                  Phone number
                  <input
                    required
                    type="tel"
                    inputMode="numeric"
                    pattern="^(?!([0-9])\\1{9})[6-9][0-9]{9}$"
                    autoComplete="tel"
                    maxLength={10}
                    title="Enter a valid 10-digit Indian mobile number starting with 6-9."
                    value={details.phone}
                    onChange={(event) =>
                      setDetails({ ...details, phone: event.target.value })
                    }
                  />
                </label>
              </div>

              <fieldset className="landing-payment-methods" disabled={submitting}>
                <legend>Payment option</legend>
                {paymentMethods.map((method) => (
                  <label key={method.code} className={gatewayConfigured && !method.enabled ? "is-unavailable" : ""}>
                    <input
                      type="radio"
                      name="paymentMethod"
                      value={method.code}
                      checked={paymentMethod === method.code}
                      disabled={submitting || (gatewayConfigured && !method.enabled)}
                      onChange={(event) =>
                        setPaymentMethod(event.target.value)
                      }
                    />
                    <span>{method.name}{!gatewayConfigured ? " (Setup required)" : !method.enabled ? " (Unavailable)" : ""}</span>
                  </label>
                ))}
              </fieldset>
              {!gatewayConfigured ? <p className="landing-payment-note">Payment options are shown but unavailable until CMS Razorpay merchant setup is complete.</p> : null}
              {paymentMethodsError ? <div className="landing-payment-error" role="alert">{paymentMethodsError}</div> : null}

              <label className="landing-payment-confirm">
                <input
                  required
                  type="checkbox"
                  checked={confirmed}
                  onChange={(event) => setConfirmed(event.target.checked)}
                />
                <span>
                  <CheckCircle2 size={18} /> I confirm the selected plan,
                  price, and payment option.
                </span>
              </label>

              {submitError && (
                <div className="landing-payment-error" role="alert">
                  {submitError}
                </div>
              )}

              <button
                className="landing-pay-button"
                type="submit"
                disabled={!hasVerifiedAdminSession() || !plan || !details.name.trim() || !details.email.trim() || !details.phone.trim() || !confirmed || submitting}
              >
                {checkingPaymentConfiguration
                  ? "Checking payment setup..."
                  : submitting
                  ? "Initiating payment..."
                  : "Initiate Payment"}{" "}
                <ArrowRight size={17} />
              </button>
              <small className="landing-payment-note">
                <ShieldCheck size={14} /> Your selection will continue through
                the existing subscription flow.
              </small>
            </form>
            )}
          </div>
        </section>
      )}
      {merchantSetupOpen ? (
        <div className="landing-merchant-modal-backdrop" role="presentation">
          <section className="landing-merchant-modal" role="dialog" aria-modal="true" aria-labelledby="merchant-setup-title">
            <h2 id="merchant-setup-title">CMS Razorpay merchant setup is required before payment can proceed.</h2>
            {/(super\s*admin|superadmin|cms\s*owner|cmsowner)/i.test(adminRole) ? (
              <p>Configure the CMS Razorpay merchant details in the authorized settings page, then retry.</p>
            ) : (
              <p>Please contact the CMS Owner to configure subscription payments.</p>
            )}
            {/(super\s*admin|superadmin|cms\s*owner|cmsowner)/i.test(adminRole) ? (
              <Link to="/superadmin/settings" className="landing-merchant-settings-link">Open merchant settings</Link>
            ) : null}
            {paymentMethodsError ? <p className="landing-payment-error" role="alert">{paymentMethodsError}</p> : null}
            <div className="landing-merchant-modal-actions">
              <button type="button" onClick={retryPaymentConfiguration} disabled={checkingPaymentConfiguration}>
                <RefreshCw size={16} /> {checkingPaymentConfiguration ? "Checking..." : "Retry"}
              </button>
              <button type="button" onClick={() => setMerchantSetupOpen(false)} disabled={checkingPaymentConfiguration}>Close</button>
            </div>
          </section>
        </div>
      ) : null}
    </main>
  );
}
