export interface Coords {
  latitude: number;
  longitude: number;
}

/** Wraps navigator.geolocation in a promise with friendly error messages. */
export function getCurrentPosition(): Promise<Coords> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new Error('Your browser does not support location. Try a different browser or device.'));
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
      (err) => {
        if (err.code === err.PERMISSION_DENIED) {
          reject(
            new Error(
              'Location access was denied. Allow location for this site in your browser settings, then try again.',
            ),
          );
        } else if (err.code === err.TIMEOUT) {
          reject(new Error('Getting your location took too long. Please try again.'));
        } else {
          reject(new Error('Your location could not be determined. Check that location services are on.'));
        }
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 30000 },
    );
  });
}
